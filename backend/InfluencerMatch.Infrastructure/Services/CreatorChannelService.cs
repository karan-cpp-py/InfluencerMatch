using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text.RegularExpressions;
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
    public class CreatorChannelService : ICreatorChannelService
    {
        private readonly ApplicationDbContext   _db;
        private readonly IHttpClientFactory     _http;
        private readonly IYouTubeQuotaTracker   _quota;
        private readonly ILogger<CreatorChannelService> _logger;
        private readonly string? _apiKey;

        // ── YouTube API response DTOs ────────────────────────────────────────
        private record YtChannelResponse(List<YtChannelItem>? items);
        private record YtChannelItem(
            string id,
            YtSnippet? snippet,
            YtStats? statistics,
            YtContentDetails? contentDetails);
        private record YtSnippet(
            string? title,
            string? description,
            string? country,
            string? publishedAt,
            YtThumbnails? thumbnails);
        private record YtStats(
            long? subscriberCount,
            long? viewCount,
            int?  videoCount);
        private record YtContentDetails(YtRelatedPlaylists? relatedPlaylists);
        private record YtRelatedPlaylists(string? uploads);
        private record YtThumbnails(YtThumb? high, YtThumb? medium, YtThumb? @default);
        private record YtThumb(string? url);

        private record YtPlaylistItemsResponse(List<YtPlaylistItem>? items);
        private record YtPlaylistItem(YtPlaylistSnippet? snippet);
        private record YtPlaylistSnippet(
            string? resourceId_videoId,
            string? title,
            string? description,
            string? publishedAt,
            YtThumbnails? thumbnails,
            YtResourceId? resourceId);
        private record YtResourceId(string? videoId);

        private record YtVideoListResponse(List<YtVideoItem>? items);
        private record YtVideoItem(
            string? id,
            YtVideoSnippet? snippet,
            YtVideoStats? statistics);
        private record YtVideoSnippet(
            string? title,
            string? description,
            string? publishedAt,
            string? categoryId,
            List<string>? tags,
            YtThumbnails? thumbnails);
        private record YtVideoStats(
            long? viewCount,
            long? likeCount,
            long? commentCount);

        public CreatorChannelService(
            ApplicationDbContext db,
            IHttpClientFactory http,
            IYouTubeQuotaTracker quota,
            ILogger<CreatorChannelService> logger,
            IConfiguration config)
        {
            _db     = db;
            _http   = http;
            _quota  = quota;
            _logger = logger;
            _apiKey = config["YouTube:ApiKey"];
        }

        // ── Public API ───────────────────────────────────────────────────────

        public async Task<CreatorChannelDto> LinkChannelAsync(
            int creatorProfileId, string channelUrl, CancellationToken ct = default)
        {
            if (!IsApiKeyConfigured())
                throw new InvalidOperationException("YouTube API key is not configured.");

            // 1. Parse URL → channel ID
            var channelId = await ResolveChannelIdAsync(channelUrl, ct);
            if (string.IsNullOrEmpty(channelId))
                throw new InvalidOperationException(
                    "Could not extract a valid YouTube channel ID from the provided URL.");

            // 2. Duplicate check
            if (await _db.CreatorChannels.AnyAsync(c => c.ChannelId == channelId, ct))
                throw new InvalidOperationException("This YouTube channel is already registered on the platform.");

            // 3. Fetch channel details
            var channelData = await FetchChannelByIdAsync(channelId, ct)
                ?? throw new InvalidOperationException("YouTube channel not found or API returned no data.");

            var thumb = channelData.snippet?.thumbnails?.high?.url
                     ?? channelData.snippet?.thumbnails?.medium?.url
                     ?? channelData.snippet?.thumbnails?.@default?.url;

            var subs = channelData.statistics?.subscriberCount ?? 0;

            var channel = new CreatorChannel
            {
                ChannelId          = channelData.id,
                CreatorProfileId   = creatorProfileId,
                ChannelName        = channelData.snippet?.title ?? string.Empty,
                Description        = TrimDesc(channelData.snippet?.description),
                ChannelUrl         = channelUrl,
                ThumbnailUrl       = thumb,
                Subscribers        = subs,
                TotalViews         = channelData.statistics?.viewCount  ?? 0,
                VideoCount         = channelData.statistics?.videoCount ?? 0,
                CreatorTier        = Creator.ComputeTier(subs),
                ChannelPublishedAt = ParseDate(channelData.snippet?.publishedAt),
                IsVerified         = false,
                CreatedAt          = DateTime.UtcNow
            };
            _db.CreatorChannels.Add(channel);
            await _db.SaveChangesAsync(ct);

            // 4b. Upsert a Creator row so analytics pipelines can process this channel
            await SyncCreatorRowAsync(creatorProfileId, channel, ct);

            // 4. Seed initial videos
            var uploadsPlaylistId = channelData.contentDetails?.relatedPlaylists?.uploads;
            if (!string.IsNullOrEmpty(uploadsPlaylistId))
                await SeedInitialVideosAsync(channel.ChannelId, uploadsPlaylistId, ct);

            return ToDto(channel);
        }

        public async Task<CreatorChannelDto?> GetChannelAsync(
            int creatorProfileId, CancellationToken ct = default)
        {
            var ch = await _db.CreatorChannels
                .FirstOrDefaultAsync(c => c.CreatorProfileId == creatorProfileId, ct);
            return ch == null ? null : ToDto(ch);
        }

        public async Task RefreshChannelStatsAsync(string channelId, CancellationToken ct = default)
        {
            if (!IsApiKeyConfigured() || !_quota.CanConsume(1)) return;

            try
            {
                var data = await FetchChannelByIdAsync(channelId, ct);
                if (data == null) return;

                var ch = await _db.CreatorChannels.FirstOrDefaultAsync(c => c.ChannelId == channelId, ct);
                if (ch == null) return;

                var subs = data.statistics?.subscriberCount ?? 0;
                ch.Subscribers          = subs;
                ch.TotalViews           = data.statistics?.viewCount  ?? 0;
                ch.VideoCount           = data.statistics?.videoCount ?? 0;
                ch.CreatorTier          = Creator.ComputeTier(subs);
                ch.LastStatsUpdatedAt   = DateTime.UtcNow;
                await _db.SaveChangesAsync(ct);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "RefreshChannelStats failed for {Id}", channelId);
            }
        }

        public async Task<List<ChannelVideoDto>> GetRecentVideosAsync(
            string channelId, int count = 10, CancellationToken ct = default)
        {
            return await _db.ChannelVideos
                .Where(v => v.ChannelId == channelId)
                .OrderByDescending(v => v.PublishedAt)
                .Take(count)
                .Select(v => new ChannelVideoDto
                {
                    YoutubeVideoId = v.YoutubeVideoId,
                    ChannelId      = v.ChannelId,
                    Title          = v.Title,
                    ThumbnailUrl   = v.ThumbnailUrl,
                    Tags           = v.Tags,
                    Category       = v.Category,
                    ViewCount      = v.ViewCount,
                    LikeCount      = v.LikeCount,
                    CommentCount   = v.CommentCount,
                    PublishedAt    = v.PublishedAt
                })
                .ToListAsync(ct);
        }

        public async Task<LiveChannelSnapshot?> FetchLiveChannelByUrlAsync(
            string channelUrl, CancellationToken ct = default)
        {
            if (!IsApiKeyConfigured())
            {
                _logger.LogWarning("FetchLiveChannelByUrlAsync: YouTube API key is not configured.");
                return null;
            }
            if (!_quota.CanConsume(1))
            {
                _logger.LogWarning("FetchLiveChannelByUrlAsync: daily quota exhausted.");
                return null;
            }
            try
            {
                var channelId = await ResolveChannelIdAsync(channelUrl, ct);
                if (string.IsNullOrEmpty(channelId))
                {
                    _logger.LogWarning("FetchLiveChannelByUrlAsync: could not resolve channel ID from URL '{Url}'", channelUrl);
                    return null;
                }

                var data = await FetchChannelByIdAsync(channelId, ct);
                if (data == null)
                {
                    _logger.LogWarning("FetchLiveChannelByUrlAsync: FetchChannelByIdAsync returned null for channelId '{Id}'", channelId);
                    return null;
                }

                var thumb = data.snippet?.thumbnails?.high?.url
                         ?? data.snippet?.thumbnails?.medium?.url
                         ?? data.snippet?.thumbnails?.@default?.url;
                var subs = data.statistics?.subscriberCount ?? 0;

                return new LiveChannelSnapshot
                {
                    ChannelId    = data.id,
                    ChannelName  = data.snippet?.title ?? string.Empty,
                    ThumbnailUrl = thumb,
                    Subscribers  = subs,
                    TotalViews   = data.statistics?.viewCount  ?? 0,
                    VideoCount   = data.statistics?.videoCount ?? 0,
                    Country      = data.snippet?.country,
                    Description  = TrimDesc(data.snippet?.description),
                    CreatorTier  = Creator.ComputeTier(subs)
                };
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "FetchLiveChannelByUrlAsync failed for {Url}", channelUrl);
                return null;
            }
        }

        // ── URL parsing + resolution ─────────────────────────────────────────

        /// <summary>
        /// Upserts a Creator row linked to the registering user so that all
        /// analytics pipelines can process this channel.
        /// </summary>
        private async Task SyncCreatorRowAsync(
            int creatorProfileId, CreatorChannel channel, CancellationToken ct)
        {
            var profile = await _db.CreatorProfiles.AsNoTracking()
                .FirstOrDefaultAsync(p => p.CreatorProfileId == creatorProfileId, ct);
            if (profile == null) return;

            var existing = await _db.Creators
                .FirstOrDefaultAsync(c => c.ChannelId == channel.ChannelId, ct);

            if (existing == null)
            {
                _db.Creators.Add(new Creator
                {
                    UserId         = profile.UserId,
                    ChannelId      = channel.ChannelId,
                    ChannelName    = channel.ChannelName,
                    Description    = channel.Description,
                    Subscribers    = channel.Subscribers,
                    TotalViews     = channel.TotalViews,
                    VideoCount     = channel.VideoCount,
                    Category       = string.Empty,
                    CreatorTier    = channel.CreatorTier ?? Creator.ComputeTier(channel.Subscribers),
                    IsSmallCreator = Creator.ComputeIsSmall(channel.Subscribers),
                    CreatedAt      = DateTime.UtcNow
                });
            }
            else
            {
                existing.UserId = profile.UserId;
                _db.Creators.Update(existing);
            }

            await _db.SaveChangesAsync(ct);
        }

        private async Task<string?> ResolveChannelIdAsync(string url, CancellationToken ct)
        {
            var trimmed = url.Trim();

            // Format 0a: bare channel ID — "UCxxxxxxxxxxxxxxxxxxxxxxxxx" (24 chars, starts with UC)
            if (Regex.IsMatch(trimmed, @"^UC[A-Za-z0-9_-]{22}$"))
                return trimmed;

            // Format 0b: bare @handle — "@SomeHandle" (no youtube.com prefix)
            if (trimmed.StartsWith("@"))
                return await ResolveViaApiAsync(
                    $"forHandle={Uri.EscapeDataString(trimmed.TrimStart('@'))}", ct);

            // Format 1: full URL /channel/UCxxx
            var m = Regex.Match(trimmed, @"youtube\.com/channel/([A-Za-z0-9_-]+)",
                RegexOptions.IgnoreCase);
            if (m.Success) return m.Groups[1].Value;

            // Format 2: full URL /@handle
            m = Regex.Match(trimmed, @"youtube\.com/@([A-Za-z0-9_.%-]+)", RegexOptions.IgnoreCase);
            if (m.Success)
                return await ResolveViaApiAsync(
                    $"forHandle={Uri.EscapeDataString(m.Groups[1].Value)}", ct);

            // Format 3: full URL /c/name or /user/name
            m = Regex.Match(trimmed, @"youtube\.com/(?:c|user)/([A-Za-z0-9_.%-]+)",
                RegexOptions.IgnoreCase);
            if (m.Success)
                return await ResolveViaApiAsync(
                    $"forUsername={Uri.EscapeDataString(m.Groups[1].Value)}", ct);

            // Format 4: bare username (no @ prefix, not a URL) — legacy fallback
            if (!trimmed.Contains('/') && !trimmed.Contains('.'))
                return await ResolveViaApiAsync(
                    $"forUsername={Uri.EscapeDataString(trimmed)}", ct);

            return null;
        }

        private async Task<string?> ResolveViaApiAsync(string queryParam, CancellationToken ct)
        {
            if (!_quota.CanConsume(1)) return null;
            try
            {
                var url = $"https://www.googleapis.com/youtube/v3/channels?part=id&{queryParam}&key={_apiKey}";
                var client = _http.CreateClient();
                using var response = await client.GetAsync(url, ct);
                if (!response.IsSuccessStatusCode)
                {
                    var body = await response.Content.ReadAsStringAsync(ct);
                    _logger.LogWarning("ResolveViaApiAsync: YouTube replied {Status} for param '{P}'. Body: {Body}",
                        (int)response.StatusCode, queryParam, body.Length > 300 ? body[..300] : body);
                    return null;
                }
                _quota.Consume(1);
                var resp = await response.Content.ReadFromJsonAsync<YtChannelResponse>(cancellationToken: ct);
                return resp?.items?.FirstOrDefault()?.id;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "ResolveViaApiAsync failed for param {P}", queryParam);
                return null;
            }
        }

        // ── YouTube API helpers ──────────────────────────────────────────────

        private async Task<YtChannelItem?> FetchChannelByIdAsync(string channelId, CancellationToken ct)
        {
            if (!_quota.CanConsume(1)) return null;
            var url = $"https://www.googleapis.com/youtube/v3/channels"
                    + $"?part=snippet,statistics,contentDetails&id={channelId}&key={_apiKey}";
            var client = _http.CreateClient();
            using var response = await client.GetAsync(url, ct);
            if (!response.IsSuccessStatusCode)
            {
                var body = await response.Content.ReadAsStringAsync(ct);
                _logger.LogWarning("FetchChannelByIdAsync: YouTube replied {Status} for channel '{Id}'. Body: {Body}",
                    (int)response.StatusCode, channelId, body.Length > 300 ? body[..300] : body);
                return null;
            }
            _quota.Consume(1);
            var resp = await response.Content.ReadFromJsonAsync<YtChannelResponse>(cancellationToken: ct);
            return resp?.items?.FirstOrDefault();
        }

        private async Task SeedInitialVideosAsync(
            string channelId, string uploadsPlaylistId, CancellationToken ct)
        {
            if (!_quota.CanConsume(2)) return;   // 1 for playlistItems + 1 for videos batch

            try
            {
                // Fetch up to 10 video IDs from uploads playlist
                var plUrl = $"https://www.googleapis.com/youtube/v3/playlistItems"
                          + $"?part=snippet&playlistId={uploadsPlaylistId}&maxResults=10&key={_apiKey}";
                var plResp = await _http.CreateClient()
                    .GetFromJsonAsync<YtPlaylistItemsResponse>(plUrl, ct);
                _quota.Consume(1);

                var videoIds = plResp?.items?
                    .Select(i => i.snippet?.resourceId?.videoId)
                    .Where(id => !string.IsNullOrEmpty(id))
                    .ToList() ?? new List<string?>();

                if (!videoIds.Any()) return;

                // Fetch video details in one Channels/Videos call
                var idsParam = string.Join(",", videoIds);
                var vidUrl   = $"https://www.googleapis.com/youtube/v3/videos"
                             + $"?part=snippet,statistics&id={idsParam}&key={_apiKey}";
                var vidResp  = await _http.CreateClient()
                    .GetFromJsonAsync<YtVideoListResponse>(vidUrl, ct);
                _quota.Consume(1);

                if (vidResp?.items == null) return;

                // Avoid inserting already-known video IDs
                var existingIds = await _db.ChannelVideos
                    .Where(v => v.ChannelId == channelId)
                    .Select(v => v.YoutubeVideoId)
                    .ToHashSetAsync(ct);

                var now = DateTime.UtcNow;
                foreach (var v in vidResp.items)
                {
                    if (v.id == null || existingIds.Contains(v.id)) continue;

                    var cv = new ChannelVideo
                    {
                        YoutubeVideoId = v.id,
                        ChannelId      = channelId,
                        Title          = v.snippet?.title ?? string.Empty,
                        Description    = TrimDesc(v.snippet?.description),
                        ThumbnailUrl   = v.snippet?.thumbnails?.high?.url
                                      ?? v.snippet?.thumbnails?.medium?.url,
                        Tags           = v.snippet?.tags != null
                                          ? string.Join(",", v.snippet.tags) : null,
                        Category       = v.snippet?.categoryId,
                        ViewCount      = v.statistics?.viewCount    ?? 0,
                        LikeCount      = v.statistics?.likeCount    ?? 0,
                        CommentCount   = v.statistics?.commentCount ?? 0,
                        PublishedAt    = ParseDate(v.snippet?.publishedAt) ?? now,
                        FetchedAt      = now
                    };
                    _db.ChannelVideos.Add(cv);

                    // seed first VideoMetrics snapshot
                    _db.VideoMetrics.Add(new VideoMetrics
                    {
                        YoutubeVideoId = v.id,
                        Views          = cv.ViewCount,
                        Likes          = cv.LikeCount,
                        Comments       = cv.CommentCount,
                        RecordedAt     = now
                    });
                }
                await _db.SaveChangesAsync(ct);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "SeedInitialVideos failed for channel {Id}", channelId);
            }
        }

        // ── Utilities ────────────────────────────────────────────────────────

        private static CreatorChannelDto ToDto(CreatorChannel c) => new()
        {
            Id                  = c.Id,
            ChannelId           = c.ChannelId,
            CreatorProfileId    = c.CreatorProfileId,
            ChannelName         = c.ChannelName,
            Description         = c.Description,
            ChannelUrl          = c.ChannelUrl,
            ThumbnailUrl        = c.ThumbnailUrl,
            Subscribers         = c.Subscribers,
            TotalViews          = c.TotalViews,
            VideoCount          = c.VideoCount,
            EngagementRate      = c.EngagementRate,
            CreatorTier         = c.CreatorTier,
            IsVerified          = c.IsVerified,
            ChannelPublishedAt  = c.ChannelPublishedAt,
            LastStatsUpdatedAt  = c.LastStatsUpdatedAt,
            CreatedAt           = c.CreatedAt
        };

        private static string? TrimDesc(string? s) =>
            s == null ? null : s[..Math.Min(500, s.Length)];

        private static DateTime? ParseDate(string? s) =>
            DateTime.TryParse(s, out var d) ? DateTime.SpecifyKind(d, DateTimeKind.Utc) : null;

        private bool IsApiKeyConfigured() =>
            !string.IsNullOrWhiteSpace(_apiKey)
            && !_apiKey.Contains("YOUR", StringComparison.OrdinalIgnoreCase)
            && !_apiKey.StartsWith("REPLACE", StringComparison.OrdinalIgnoreCase);
    }
}
