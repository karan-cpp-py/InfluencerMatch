using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Json;
using System.Threading.Tasks;
using InfluencerMatch.Application.DTOs;
using InfluencerMatch.Application.Interfaces;
using InfluencerMatch.Domain.Entities;
using InfluencerMatch.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace InfluencerMatch.Infrastructure.Services
{
    /// <summary>
    /// Calculates per-creator analytics using data already stored in the database
    /// combined with per-video stats fetched from the YouTube Data API.
    /// </summary>
    public class CreatorAnalyticsService : ICreatorAnalyticsService
    {
        private readonly ICreatorRepository _repo;
        private readonly ApplicationDbContext _db;
        private readonly IHttpClientFactory _httpFactory;
        private readonly ILogger<CreatorAnalyticsService> _logger;
        private readonly string? _apiKey;

        public CreatorAnalyticsService(
            ICreatorRepository repo,
            ApplicationDbContext db,
            IHttpClientFactory httpFactory,
            ILogger<CreatorAnalyticsService> logger,
            IConfiguration config)
        {
            _repo       = repo;
            _db         = db;
            _httpFactory = httpFactory;
            _logger     = logger;
            _apiKey     = config["YouTube:ApiKey"];
        }

        // ─────────────────────────────────────────────────────────
        // Public service methods
        // ─────────────────────────────────────────────────────────

        public async Task<CreatorAnalyticsDto?> GetCreatorAnalyticsAsync(int creatorId)
        {
            var creator = await _db.Creators.AsNoTracking()
                .FirstOrDefaultAsync(c => c.CreatorId == creatorId);
            if (creator == null) return null;

            var analytics = await _repo.GetLatestAnalyticsAsync(creatorId);
            var growthRaw = await _repo.GetGrowthHistoryAsync(creatorId, 30);

            // Build growth delta series
            var growthHistory = growthRaw
                .OrderBy(g => g.RecordedAt)
                .Select((g, i) => new GrowthPointDto
                {
                    RecordedAt  = g.RecordedAt,
                    Subscribers = g.Subscribers,
                    Delta       = i == 0 ? null : g.Subscribers - growthRaw.OrderBy(x => x.RecordedAt).ElementAt(i - 1).Subscribers
                })
                .OrderByDescending(g => g.RecordedAt)
                .ToList();

            // Fetch top videos from YouTube API if key is available
            var topVideos = await FetchTopVideosAsync(creator.ChannelId, 5);

            return new CreatorAnalyticsDto
            {
                CreatorId             = creator.CreatorId,
                ChannelId             = creator.ChannelId,
                ChannelName           = creator.ChannelName ?? string.Empty,
                Platform              = creator.Platform,
                Category              = creator.Category ?? string.Empty,
                Subscribers           = creator.Subscribers,
                TotalVideos           = creator.VideoCount,
                AverageViews          = analytics?.AvgViews          ?? EstimateAvgViews(creator),
                AverageLikes          = analytics?.AvgLikes          ?? 0,
                AverageComments       = analytics?.AvgComments       ?? 0,
                EngagementRate        = EngagementRateEstimator.EstimateOrStored(
                    analytics?.EngagementRate,
                    creator.Subscribers,
                    creator.TotalViews,
                    creator.VideoCount,
                    analytics?.AvgViews),
                AnalyticsCalculatedAt = analytics?.CalculatedAt,
                GrowthHistory         = growthHistory,
                TopVideos             = topVideos
            };
        }

        public async Task<bool> RefreshAnalyticsAsync(int creatorId)
        {
            var creator = await _db.Creators.AsNoTracking()
                .FirstOrDefaultAsync(c => c.CreatorId == creatorId);
            if (creator == null) return false;

            var (avgViews, avgLikes, avgComments) = await FetchVideoStatsAsync(creator.ChannelId);

            double engagementRate = avgViews > 0
                ? Math.Round((avgLikes + avgComments) / avgViews, 6)
                : 0;

            await _repo.UpsertAnalyticsAsync(new CreatorAnalytics
            {
                CreatorId      = creatorId,
                AvgViews       = avgViews,
                AvgLikes       = avgLikes,
                AvgComments    = avgComments,
                EngagementRate = engagementRate,
                CalculatedAt   = DateTime.UtcNow
            });

            _logger.LogDebug("Analytics refreshed for creator {CreatorId}: ER={ER:P2}", creatorId, engagementRate);
            return true;
        }

        public async Task RefreshAllAnalyticsAsync()
        {
            var creatorIds = await _db.Creators.Where(c => c.UserId != null).Select(c => c.CreatorId).ToListAsync();
            _logger.LogInformation("Refreshing analytics for {Count} creators", creatorIds.Count);
            foreach (var id in creatorIds)
            {
                try   { await RefreshAnalyticsAsync(id); }
                catch (HttpRequestException httpEx) when (
                    httpEx.StatusCode is System.Net.HttpStatusCode.Forbidden
                                       or System.Net.HttpStatusCode.TooManyRequests
                                       or System.Net.HttpStatusCode.Unauthorized)
                {
                    _logger.LogWarning(
                        "CreatorAnalytics: YouTube API returned {Code} — stopping batch. Check API key and daily quota.",
                        (int?)httpEx.StatusCode);
                    break;
                }
                catch (Exception ex) { _logger.LogError(ex, "Failed to refresh analytics for creator {Id}", id); }
            }
        }

        public async Task<PagedResultDto<CreatorSearchResultDto>> SearchCreatorsAsync(CreatorSearchQueryDto query)
            => await _repo.SearchAsync(query);

        public async Task RecordGrowthSnapshotAsync()
        {
            var creators = await _db.Creators.AsNoTracking().Where(c => c.UserId != null).ToListAsync();
            _logger.LogInformation("Recording growth snapshots for {Count} creators", creators.Count);
            foreach (var creator in creators)
            {
                try
                {
                    await _repo.AddGrowthSnapshotAsync(new CreatorGrowth
                    {
                        CreatorId  = creator.CreatorId,
                        Subscribers = creator.Subscribers,
                        RecordedAt = DateTime.UtcNow
                    });
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Failed to record growth snapshot for creator {Id}", creator.CreatorId);
                }
            }
        }

        // ─────────────────────────────────────────────────────────
        // Private YouTube Data API helpers
        // ─────────────────────────────────────────────────────────

        private bool IsApiKeyConfigured() =>
            !string.IsNullOrWhiteSpace(_apiKey) &&
            !_apiKey!.Contains("YOUR", StringComparison.OrdinalIgnoreCase) &&
            !_apiKey.StartsWith("REPLACE", StringComparison.OrdinalIgnoreCase);

        /// <summary>
        /// Fetch statistics for the 10 most-recent videos of a channel and
        /// return average views / likes / comments.
        /// Falls back to channel-level estimates when the API key is not set.
        /// </summary>
        private async Task<(double avgViews, double avgLikes, double avgComments)> FetchVideoStatsAsync(string channelId)
        {
            if (!IsApiKeyConfigured())
            {
                _logger.LogWarning("YouTube API key not configured – using channel-level estimates for {ChannelId}", channelId);
                var creator = await _db.Creators.AsNoTracking().FirstOrDefaultAsync(c => c.ChannelId == channelId);
                if (creator != null)
                {
                    double est = EstimateAvgViews(creator);
                    return (est, est * 0.03, est * 0.005); // industry avg ratios
                }
                return (0, 0, 0);
            }

            var client = _httpFactory.CreateClient();

            // Check response status BEFORE the swallowing try/catch so 403 propagates to break the batch
            var uploadsId    = UploadsPlaylistId(channelId);
            var playlistUrl  = $"https://www.googleapis.com/youtube/v3/playlistItems"
                             + $"?part=contentDetails&playlistId={uploadsId}&maxResults=10&key={_apiKey}";
            var plHttpResp   = await client.GetAsync(playlistUrl);
            if (plHttpResp.StatusCode is System.Net.HttpStatusCode.Forbidden
                                        or System.Net.HttpStatusCode.TooManyRequests
                                        or System.Net.HttpStatusCode.Unauthorized)
                throw new HttpRequestException($"YouTube API returned {(int)plHttpResp.StatusCode}", null, plHttpResp.StatusCode);

            try
            {
                // PlaylistItems API: 1 unit (vs Search API: 100 units)
                var playlistResp = await plHttpResp.Content.ReadFromJsonAsync<YtPlaylistItemsResponse>();
                if (playlistResp?.items == null || !playlistResp.items.Any())
                    return (0, 0, 0);

                var videoIds = string.Join(",", playlistResp.items
                    .Select(i => i.contentDetails?.videoId).Where(v => v != null));
                if (string.IsNullOrEmpty(videoIds)) return (0, 0, 0);

                // Video statistics (1 unit)
                var statsUrl = $"https://www.googleapis.com/youtube/v3/videos"
                             + $"?part=statistics&id={videoIds}&key={_apiKey}";
                var statsResp = await client.GetFromJsonAsync<YtVideoStatsResponse>(statsUrl);
                if (statsResp?.items == null || !statsResp.items.Any())
                    return (0, 0, 0);

                double avgViews    = statsResp.items.Average(v => (double)(v.statistics?.viewCount    ?? 0));
                double avgLikes    = statsResp.items.Average(v => (double)(v.statistics?.likeCount    ?? 0));
                double avgComments = statsResp.items.Average(v => (double)(v.statistics?.commentCount ?? 0));

                return (Math.Round(avgViews, 2), Math.Round(avgLikes, 2), Math.Round(avgComments, 2));
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "YouTube API error fetching video stats for channel {ChannelId}; falling back to estimates", channelId);
                // Fall back to channel-level estimates so analytics aren't all zeros
                var creator = await _db.Creators.AsNoTracking().FirstOrDefaultAsync(c => c.ChannelId == channelId);
                if (creator != null)
                {
                    double est = EstimateAvgViews(creator);
                    return (est, Math.Round(est * 0.03, 2), Math.Round(est * 0.005, 2));
                }
                return (0, 0, 0);
            }
        }

        private async Task<List<TopVideoDto>> FetchTopVideosAsync(string channelId, int count)
        {
            if (!IsApiKeyConfigured()) return new List<TopVideoDto>();
            var client = _httpFactory.CreateClient();
            try
            {
                // PlaylistItems API: 1 unit (vs Search API: 100 units)
                // Fetch more items than needed so we can sort by view count and return true top videos
                var uploadsId   = UploadsPlaylistId(channelId);
                var fetchCount  = Math.Max(count * 3, 15);
                var playlistUrl = $"https://www.googleapis.com/youtube/v3/playlistItems"
                                + $"?part=snippet&playlistId={uploadsId}&maxResults={fetchCount}&key={_apiKey}";
                var playlistResp = await client.GetFromJsonAsync<YtPlaylistItemsResponse>(playlistUrl);
                if (playlistResp?.items == null) return new List<TopVideoDto>();

                var videoMeta = playlistResp.items
                    .Select(i => new {
                        VideoId = i.snippet?.resourceId?.videoId,
                        Title   = i.snippet?.title,
                        Thumb   = i.snippet?.thumbnails?.medium?.url
                    })
                    .Where(x => x.VideoId != null)
                    .ToDictionary(x => x.VideoId!, x => x);

                if (videoMeta.Count == 0) return new List<TopVideoDto>();

                // Videos API: fetch statistics to sort by view count (1 unit)
                var statsUrl  = $"https://www.googleapis.com/youtube/v3/videos"
                              + $"?part=statistics&id={string.Join(",", videoMeta.Keys)}&key={_apiKey}";
                var statsResp = await client.GetFromJsonAsync<YtVideoStatsResponse>(statsUrl);

                if (statsResp?.items == null)
                {
                    return videoMeta.Values.Take(count).Select(v => new TopVideoDto
                    {
                        VideoId      = v.VideoId!,
                        Title        = v.Title ?? string.Empty,
                        ThumbnailUrl = v.Thumb ?? string.Empty
                    }).ToList();
                }

                return statsResp.items
                    .Where(i => i.id != null && videoMeta.ContainsKey(i.id))
                    .OrderByDescending(i => i.statistics?.viewCount ?? 0)
                    .Take(count)
                    .Select(i => new TopVideoDto
                    {
                        VideoId      = i.id!,
                        Title        = videoMeta[i.id!].Title ?? string.Empty,
                        ThumbnailUrl = videoMeta[i.id!].Thumb ?? string.Empty,
                        Views        = i.statistics?.viewCount ?? 0,
                        Likes        = i.statistics?.likeCount ?? 0
                    })
                    .ToList();
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to fetch top videos for {ChannelId}", channelId);
                return new List<TopVideoDto>();
            }
        }

        private static double EstimateAvgViews(Creator c) =>
            c.VideoCount > 0 ? Math.Round((double)c.TotalViews / c.VideoCount, 2) : 0;

        // ─────────────────────────────────────────────────────────
        // Helpers
        // ─────────────────────────────────────────────────────────

        /// <summary>
        /// Derives the uploads-playlist ID from a channel ID by swapping the 'UC' prefix to 'UU'.
        /// This is the standard YouTube convention; avoids the expensive Search API call.
        /// </summary>
        private static string UploadsPlaylistId(string channelId) =>
            channelId.StartsWith("UC", StringComparison.Ordinal)
                ? "UU" + channelId[2..]
                : channelId;

        // ─────────────────────────────────────────────────────────
        // YouTube API response DTOs (internal)
        // ─────────────────────────────────────────────────────────

        // PlaylistItems API — used to list a channel's recent uploads at 1 quota unit
        private class YtPlaylistItemsResponse
        {
            public List<PlaylistItem>? items { get; set; }
            public class PlaylistItem
            {
                public ItemContentDetails? contentDetails { get; set; }
                public ItemSnippet?        snippet        { get; set; }
            }
            public class ItemContentDetails
            {
                public string?   videoId          { get; set; }
                public DateTime? videoPublishedAt { get; set; }
            }
            public class ItemSnippet
            {
                public string?     title       { get; set; }
                public DateTime?   publishedAt { get; set; }
                public Thumbnails? thumbnails  { get; set; }
                public ResourceId? resourceId  { get; set; }
            }
            public class ResourceId { public string? videoId { get; set; } }
            public class Thumbnails { public Thumb?  medium  { get; set; } }
            public class Thumb      { public string? url     { get; set; } }
        }

        private class YtSearchResponse
        {
            public List<SearchItem>? items { get; set; }
            public class SearchItem
            {
                public VideoId? id { get; set; }
                public Snippet? snippet { get; set; }
            }
            public class VideoId
            {
                public string? videoId { get; set; }
            }
            public class Snippet
            {
                public string? title { get; set; }
                public Thumbnails? thumbnails { get; set; }
            }
            public class Thumbnails
            {
                public Thumb? medium { get; set; }
            }
            public class Thumb
            {
                public string? url { get; set; }
            }
        }

        private class YtVideoStatsResponse
        {
            public List<VideoItem>? items { get; set; }
            public class VideoItem
            {
                public string?     id         { get; set; }   // populated by videos.list
                public VideoStats? statistics { get; set; }
            }
            public class VideoStats
            {
                public long? viewCount    { get; set; }
                public long? likeCount    { get; set; }
                public long? commentCount { get; set; }
            }
        }
    }
}
