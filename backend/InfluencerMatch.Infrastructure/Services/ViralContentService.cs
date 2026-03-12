using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Json;
using System.Threading;
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
    /// Viral Content Prediction — Feature 5.
    ///
    /// Raw metrics per video:
    ///   ViewsVelocity_raw      = ViewCount  / max(HoursSincePublish, 1)
    ///   GrowthAcceleration_raw = ViewCount  / max(HoursSincePublish², 1)
    ///   EngagementMomentum_raw = (LikeCount + CommentCount) / max(ViewCount, 1)
    ///
    /// Batch-normalise each metric to 0-1 then:
    ///   ViralScore = 0.5×Velocity + 0.3×Acceleration + 0.2×Engagement
    ///
    /// Data source: YouTube API when available; CreatorAnalytics fallback otherwise.
    /// </summary>
    public class ViralContentService : IViralContentService
    {
        private readonly ApplicationDbContext _db;
        private readonly IHttpClientFactory _httpFactory;
        private readonly ILogger<ViralContentService> _logger;
        private readonly string? _apiKey;

        // Restrict YouTube API calls to top N creators by subscribers
        private const int ApiCreatorLimit = 20;
        private const int VideosPerCreator = 5;

        public ViralContentService(
            ApplicationDbContext db,
            IHttpClientFactory httpFactory,
            ILogger<ViralContentService> logger,
            IConfiguration config)
        {
            _db         = db;
            _httpFactory = httpFactory;
            _logger     = logger;
            _apiKey     = config["YouTube:ApiKey"];
        }

        // ── RefreshViralScoresAsync ───────────────────────────────────────────

        public async Task RefreshViralScoresAsync(CancellationToken ct = default)
        {
            _logger.LogInformation("ViralContentService: starting viral score refresh");

            if (IsApiKeyConfigured())
            {
                var fetched = await TryFetchYouTubeVideosAsync(ct);
                if (fetched > 0)
                {
                    _logger.LogInformation("ViralContentService: scored {Count} YouTube videos", fetched);
                    return;
                }
            }

            // Fallback: derive virtual video rows from stored CreatorAnalytics
            await RefreshFromAnalyticsFallbackAsync(ct);
        }

        // ── GetTrendingVideosAsync ────────────────────────────────────────────

        public async Task<List<TrendingVideoDto>> GetTrendingVideosAsync(
            int topN = 50,
            string? category = null,
            string? country = null)
        {
            if (string.IsNullOrWhiteSpace(category)) category = null;
            if (string.IsNullOrWhiteSpace(country))  country  = null;

            var query =
                from vs in _db.VideoViralScores.AsNoTracking()
                join v  in _db.Videos.AsNoTracking()   on vs.VideoId     equals v.VideoId
                join c  in _db.Creators.AsNoTracking() on v.CreatorId    equals c.CreatorId
                where (category == null || c.Category == category)
                   && (country  == null || c.Country  == country)
                orderby vs.ViralScore descending
                select new TrendingVideoDto
                {
                    VideoId           = v.VideoId,
                    Title             = v.Title,
                    ThumbnailUrl      = v.ThumbnailUrl,
                    PublishedAt       = v.PublishedAt,
                    HoursSincePublish = Math.Round(
                        (vs.CalculatedAt - v.PublishedAt).TotalHours, 1),
                    CreatorId         = c.CreatorId,
                    ChannelName       = c.ChannelName ?? string.Empty,
                    Platform          = c.Platform,
                    Category          = c.Category    ?? string.Empty,
                    Country           = c.Country     ?? string.Empty,
                    Subscribers       = c.Subscribers,
                    ViewCount         = v.ViewCount,
                    LikeCount         = v.LikeCount,
                    CommentCount      = v.CommentCount,
                    ViewsVelocity     = vs.ViewsVelocity,
                    GrowthAcceleration = vs.GrowthAcceleration,
                    EngagementMomentum = vs.EngagementMomentum,
                    ViralScore        = vs.ViralScore,
                    CalculatedAt      = vs.CalculatedAt
                };

            var rows = await query.Take(topN).ToListAsync();

            // If table is empty, compute on-the-fly from CreatorAnalytics
            if (rows.Count == 0)
            {
                _logger.LogInformation("ViralContentService: no scored videos found, computing on-the-fly");
                await RefreshFromAnalyticsFallbackAsync();
                rows = await query.Take(topN).ToListAsync();
            }

            return rows;
        }

        // ── YouTube API path ─────────────────────────────────────────────────

        private async Task<int> TryFetchYouTubeVideosAsync(CancellationToken ct)
        {
            var creators = await _db.Creators.AsNoTracking()
                .Where(c => c.ChannelId != null && c.ChannelId != "")
                .OrderByDescending(c => c.Subscribers)
                .Take(ApiCreatorLimit)
                .ToListAsync(ct);

            var client = _httpFactory.CreateClient();
            var allRawVideos = new List<(Creator creator, string videoId,
                string title, string? thumb, DateTime publishedAt,
                long views, long likes, long comments)>();

            foreach (var creator in creators)
            {
                if (ct.IsCancellationRequested) break;
                try
                {
                    var videos = await FetchCreatorVideosAsync(client, creator, ct);
                    allRawVideos.AddRange(videos);
                }
                catch (HttpRequestException httpEx) when (
                    httpEx.StatusCode is System.Net.HttpStatusCode.Forbidden
                                       or System.Net.HttpStatusCode.TooManyRequests
                                       or System.Net.HttpStatusCode.Unauthorized)
                {
                    _logger.LogWarning("ViralContent: YouTube API returned {Code} — stopping batch.", (int?)httpEx.StatusCode);
                    break;
                }
            }

            if (allRawVideos.Count == 0) return 0;

            await UpsertVideosAndScoresAsync(allRawVideos, ct);
            return allRawVideos.Count;
        }

        private async Task<List<(Creator, string, string, string?, DateTime, long, long, long)>>
            FetchCreatorVideosAsync(HttpClient client, Creator creator, CancellationToken ct)
        {
            var result = new List<(Creator, string, string, string?, DateTime, long, long, long)>();
            try
            {
                // ─ PlaylistItems API: 1 unit (vs Search API: 100 units) ────────────────
                // Uploads playlist = channel ID with 'UC' → 'UU' prefix swap
                var uploadsId    = UploadsPlaylistId(creator.ChannelId);
                var playlistUrl  = $"https://www.googleapis.com/youtube/v3/playlistItems"
                                 + $"?part=snippet&playlistId={uploadsId}&maxResults={VideosPerCreator}&key={_apiKey}";

                var playlistResp = await client.GetFromJsonAsync<YtPlaylistListResponse>(playlistUrl, ct);
                if (playlistResp?.items == null || playlistResp.items.Count == 0) return result;

                // Filter client-side to last 7 days
                var cutoff   = DateTime.UtcNow.AddDays(-7);
                var idToMeta = playlistResp.items
                    .Where(i => i.snippet?.resourceId?.videoId != null &&
                                (i.snippet.publishedAt ?? DateTime.MinValue) >= cutoff)
                    .ToDictionary(
                        i => i.snippet!.resourceId!.videoId!,
                        i => i.snippet!);

                if (idToMeta.Count == 0) return result;

                // ─ Videos API statistics: 1 unit ───────────────────────────────────────
                var statsUrl  = $"https://www.googleapis.com/youtube/v3/videos"
                              + $"?part=statistics&id={string.Join(",", idToMeta.Keys)}&key={_apiKey}";
                var statsResp = await client.GetFromJsonAsync<YtVideoStatsResponse>(statsUrl, ct);
                if (statsResp?.items == null) return result;

                foreach (var item in statsResp.items)
                {
                    if (item.id == null || !idToMeta.TryGetValue(item.id, out var snip)) continue;

                    DateTime publishedAt = snip.publishedAt  ?? DateTime.UtcNow.AddDays(-1);
                    string   title       = snip.title        ?? "(untitled)";
                    string?  thumb       = snip.thumbnails?.medium?.url;
                    long views    = item.statistics?.viewCount    ?? 0;
                    long likes    = item.statistics?.likeCount    ?? 0;
                    long comments = item.statistics?.commentCount ?? 0;

                    result.Add((creator, item.id, title, thumb, publishedAt, views, likes, comments));
                }
            }
            catch (OperationCanceledException) { throw; }
            catch (HttpRequestException) { throw; }  // let 403/429 propagate to break the outer loop
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "ViralContentService: YouTube fetch failed for {ChannelId}", creator.ChannelId);
            }
            return result;
        }

        // ── Fallback: derive virtual videos from CreatorAnalytics ─────────────

        private async Task RefreshFromAnalyticsFallbackAsync(CancellationToken ct = default)
        {
            _logger.LogInformation("ViralContentService: using CreatorAnalytics fallback");

            var creators = await _db.Creators.AsNoTracking()
                .Where(c => c.ChannelId != null && c.ChannelId != "")
                .ToListAsync(ct);
            var analyticsMap = await _db.CreatorAnalytics.AsNoTracking()
                .ToDictionaryAsync(a => a.CreatorId, ct);

            var rawVideos = new List<(Creator creator, string videoId,
                string title, string? thumb, DateTime publishedAt,
                long views, long likes, long comments)>();

            foreach (var c in creators)
            {
                if (!analyticsMap.TryGetValue(c.CreatorId, out var a)) continue;

                // Synthetic video ID so it's stable across refreshes
                string syntheticId = $"SYNTHETIC_{c.CreatorId}";
                // Treat analytics as a "virtual video" published 24h ago
                DateTime fakePublish = a.CalculatedAt.AddHours(-24);

                rawVideos.Add((
                    c,
                    syntheticId,
                    $"Latest videos — {c.ChannelName}",
                    null,
                    fakePublish,
                    (long)a.AvgViews,
                    (long)a.AvgLikes,
                    (long)a.AvgComments
                ));
            }

            if (rawVideos.Count == 0) return;

            await UpsertVideosAndScoresAsync(rawVideos, ct);
        }

        // ── Score computation + persistence ──────────────────────────────────

        private async Task UpsertVideosAndScoresAsync(
            List<(Creator creator, string videoId, string title, string? thumb,
                  DateTime publishedAt, long views, long likes, long comments)> batch,
            CancellationToken ct = default)
        {
            var now = DateTime.UtcNow;

            // 1. Compute raw metrics for every video in the batch
            var rawMetrics = batch.Select(r =>
            {
                double hours  = Math.Max((now - r.publishedAt).TotalHours, 1.0);
                double vel    = r.views / hours;
                double accel  = r.views / (hours * hours);
                double eng    = r.views > 0 ? (r.likes + r.comments) / (double)r.views : 0;
                return (r, vel, accel, eng);
            }).ToList();

            // 2. Batch-normalise each metric 0-1
            double maxVel   = rawMetrics.Max(x => x.vel);
            double maxAcc   = rawMetrics.Max(x => x.accel);
            double maxEng   = rawMetrics.Max(x => x.eng);

            // 3. Upsert Videos + VideoViralScores
            foreach (var (r, vel, accel, eng) in rawMetrics)
            {
                if (ct.IsCancellationRequested) break;

                double nVel   = maxVel   > 0 ? vel   / maxVel   : 0;
                double nAcc   = maxAcc   > 0 ? accel / maxAcc   : 0;
                double nEng   = maxEng   > 0 ? eng   / maxEng   : 0;
                double score  = Math.Round(0.5 * nVel + 0.3 * nAcc + 0.2 * nEng, 6);

                // Upsert Video row
                var video = await _db.Videos.FirstOrDefaultAsync(v => v.VideoId == r.videoId, ct);
                if (video == null)
                {
                    video = new Video
                    {
                        VideoId     = r.videoId,
                        CreatorId   = r.creator.CreatorId,
                        Title       = r.title,
                        ThumbnailUrl = r.thumb,
                        ViewCount   = r.views,
                        LikeCount   = r.likes,
                        CommentCount = r.comments,
                        PublishedAt = r.publishedAt,
                        FetchedAt   = now
                    };
                    _db.Videos.Add(video);
                }
                else
                {
                    video.ViewCount    = r.views;
                    video.LikeCount    = r.likes;
                    video.CommentCount = r.comments;
                    video.FetchedAt    = now;
                    _db.Videos.Update(video);
                }

                // Upsert VideoViralScore row
                var vs = await _db.VideoViralScores
                    .FirstOrDefaultAsync(s => s.VideoId == r.videoId, ct);
                if (vs == null)
                {
                    _db.VideoViralScores.Add(new VideoViralScore
                    {
                        VideoId            = r.videoId,
                        ViralScore         = score,
                        ViewsVelocityRaw   = Math.Round(vel, 4),
                        ViewsVelocity      = Math.Round(nVel,  6),
                        GrowthAcceleration = Math.Round(nAcc,  6),
                        EngagementMomentum = Math.Round(nEng,  6),
                        CalculatedAt       = now
                    });
                }
                else
                {
                    vs.ViralScore         = score;
                    vs.ViewsVelocityRaw   = Math.Round(vel,  4);
                    vs.ViewsVelocity      = Math.Round(nVel, 6);
                    vs.GrowthAcceleration = Math.Round(nAcc, 6);
                    vs.EngagementMomentum = Math.Round(nEng, 6);
                    vs.CalculatedAt       = now;
                    _db.VideoViralScores.Update(vs);
                }

                await _db.SaveChangesAsync(ct);
            }
        }

        // ── Helpers ──────────────────────────────────────────────────────────

        /// <summary>
        /// Derives the uploads-playlist ID from a channel ID:
        /// replaces the 'UC' prefix with 'UU' (standard YouTube convention).
        /// Costs 0 quota units — no extra API call needed.
        /// </summary>
        private static string UploadsPlaylistId(string channelId) =>
            channelId.StartsWith("UC", StringComparison.Ordinal)
                ? "UU" + channelId[2..]
                : channelId;

        private bool IsApiKeyConfigured() =>
            !string.IsNullOrWhiteSpace(_apiKey) &&
            !_apiKey!.Contains("YOUR", StringComparison.OrdinalIgnoreCase) &&
            !_apiKey.StartsWith("REPLACE", StringComparison.OrdinalIgnoreCase);

        // ── Internal YouTube API response shapes ──────────────────────────────────────────

        // PlaylistItems response — 1 quota unit to list recent uploads
        private class YtPlaylistListResponse
        {
            public List<PlaylistItem>? items { get; set; }
            public class PlaylistItem                { public PlSnippet?  snippet { get; set; } }
            public class PlSnippet
            {
                public string?    title       { get; set; }
                public DateTime?  publishedAt { get; set; }
                public PlThumbs?  thumbnails  { get; set; }
                public PlResId?   resourceId  { get; set; }
            }
            public class PlResId  { public string? videoId { get; set; } }
            public class PlThumbs { public PlThumb? medium  { get; set; } }
            public class PlThumb  { public string?  url     { get; set; } }
        }

        private class YtVideoStatsResponse
        {
            public List<VideoItem>? items { get; set; }
            public class VideoItem
            {
                public string? id { get; set; }
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
