using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json;
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
    /// <summary>
    /// Scans YouTube video titles, descriptions and tags for brand-promotion signals.
    ///
    /// Detection tiers:
    ///   Tier 1 – Hashtag (#ad, #sponsored, #partner, #advertisement, #collab) → confidence 0.95
    ///   Tier 2 – @-mention (any @handle in description)                       → confidence 0.80
    ///   Tier 3 – Keyword phrase ("sponsored by", "in partnership with", …)    → confidence 0.70
    /// </summary>
    public class BrandPromotionService : IBrandPromotionService
    {
        // ── Detection data ──────────────────────────────────────────────────
        private static readonly HashSet<string> SponsorHashtags = new(StringComparer.OrdinalIgnoreCase)
            { "#ad", "#sponsored", "#partner", "#advertisement", "#collab", "#gifted", "#promo" };

        private static readonly Regex MentionRegex = new(
            @"(?<!\w)@([A-Za-z0-9_]{2,50})", RegexOptions.Compiled | RegexOptions.IgnoreCase);

        private static readonly string[] KeywordPhrases =
        {
            "sponsored by", "in partnership with", "ad:", "paid promotion",
            "brought to you by", "thanks to", "in collaboration with",
            "working with", "paid partnership"
        };

        // ── Fields ──────────────────────────────────────────────────────────
        private readonly ApplicationDbContext _db;
        private readonly IHttpClientFactory   _httpFactory;
        private readonly ILogger<BrandPromotionService> _logger;
        private readonly string? _apiKey;

        public BrandPromotionService(
            ApplicationDbContext   db,
            IHttpClientFactory     httpFactory,
            ILogger<BrandPromotionService> logger,
            IConfiguration         config)
        {
            _db          = db;
            _httpFactory = httpFactory;
            _logger      = logger;
            _apiKey =
                config["YouTube:ApiKey"]
                ?? config["YouTube__ApiKey"]
                ?? config["YOUTUBE_API_KEY"];
        }

        // ────────────────────────────────────────────────────────────────────
        // Public interface implementation
        // ────────────────────────────────────────────────────────────────────

        public async Task<int> ScanCreatorAsync(int creatorId, CancellationToken ct = default)
        {
            var creator = await _db.Creators.AsNoTracking()
                .FirstOrDefaultAsync(c => c.CreatorId == creatorId, ct);
            if (creator == null) return 0;

            var videos = await FetchRecentVideosAsync(creator.ChannelId, 10, ct);
            if (videos.Count == 0) return 0;

            var existing = await _db.BrandMentions
                .Where(m => m.CreatorId == creatorId)
                .Select(m => m.VideoId)
                .ToHashSetAsync(cancellationToken: ct);

            int newCount = 0;
            var now = DateTime.UtcNow;

            foreach (var v in videos)
            {
                if (existing.Contains(v.VideoId)) continue;   // already scanned

                var detections = Detect(v.Title, v.Description, v.Tags);
                foreach (var det in detections)
                {
                    _db.BrandMentions.Add(new BrandMention
                    {
                        VideoId          = v.VideoId,
                        VideoTitle       = v.Title,
                        CreatorId        = creatorId,
                        BrandName        = det.BrandName,
                        DetectionMethod  = det.Method,
                        ConfidenceScore  = det.Confidence,
                        DetectedAt       = now
                    });
                    newCount++;
                }
            }

            if (newCount > 0)
                await _db.SaveChangesAsync(ct);

            _logger.LogDebug("Creator {Id}: scanned {V} videos, stored {N} new brand mentions",
                creatorId, videos.Count, newCount);
            return newCount;
        }

        public async Task ScanAllCreatorsAsync(CancellationToken ct = default)
        {
            var ids = await _db.Creators.Where(c => c.UserId != null).Select(c => c.CreatorId).ToListAsync(ct);
            _logger.LogInformation("Brand-promotion scan starting for {N} creators", ids.Count);

            int total = 0;
            foreach (var id in ids)
            {
                if (ct.IsCancellationRequested) break;
                try
                {
                    total += await ScanCreatorAsync(id, ct);
                }
                catch (HttpRequestException httpEx) when (
                    httpEx.StatusCode is System.Net.HttpStatusCode.Forbidden
                                       or System.Net.HttpStatusCode.TooManyRequests
                                       or System.Net.HttpStatusCode.Unauthorized)
                {
                    _logger.LogWarning("Brand scan: YouTube API returned {Code} — stopping batch.", (int?)httpEx.StatusCode);
                    break;
                }
                catch (Exception ex) { _logger.LogWarning(ex, "Scan failed for creator {Id}", id); }
                await Task.Delay(300, ct); // polite rate limiting
            }

            _logger.LogInformation("Brand-promotion scan complete. {N} new mentions stored", total);
        }

        public async Task<List<BrandMentionDto>> GetMentionsForBrandAsync(string brandName)
        {
            var lower = brandName.ToLowerInvariant().TrimStart('#', '@');

            return await _db.BrandMentions
                .AsNoTracking()
                .Include(m => m.Creator)
                .Where(m => m.BrandName.ToLower().Contains(lower))
                .OrderByDescending(m => m.DetectedAt)
                .Select(m => new BrandMentionDto
                {
                    BrandMentionId  = m.BrandMentionId,
                    VideoId         = m.VideoId,
                    VideoTitle      = m.VideoTitle,
                    CreatorId       = m.CreatorId,
                    ChannelName     = m.Creator.ChannelName ?? string.Empty,
                    BrandName       = m.BrandName,
                    DetectionMethod = m.DetectionMethod,
                    ConfidenceScore = m.ConfidenceScore,
                    DetectedAt      = m.DetectedAt
                })
                .ToListAsync();
        }

        public async Task<BrandAnalyticsDto?> GetBrandAnalyticsAsync(string brandName)
        {
            var lower = brandName.ToLowerInvariant().TrimStart('#', '@');

            var mentions = await _db.BrandMentions
                .AsNoTracking()
                .Include(m => m.Creator)
                .Where(m => m.BrandName.ToLower().Contains(lower))
                .ToListAsync();

            if (mentions.Count == 0) return null;

            // Join with analytics for view/engagement estimates
            var creatorIds = mentions.Select(m => m.CreatorId).Distinct().ToList();
            var analyticsMap = await _db.CreatorAnalytics
                .AsNoTracking()
                .Where(a => creatorIds.Contains(a.CreatorId))
                .ToDictionaryAsync(a => a.CreatorId);

            var grouped = mentions
                .GroupBy(m => m.CreatorId)
                .Select(g =>
                {
                    var creator    = g.First().Creator;
                    analyticsMap.TryGetValue(g.Key, out var a);
                    double avgViews = a?.AvgViews ?? EstimateAvgViews(creator.TotalViews, creator.VideoCount);
                    double eng      = a?.EngagementRate ?? 0;

                    return new BrandPromotingCreatorDto
                    {
                        CreatorId      = g.Key,
                        ChannelName    = creator.ChannelName ?? string.Empty,
                        Platform       = creator.Platform,
                        Category       = creator.Category ?? string.Empty,
                        Subscribers    = creator.Subscribers,
                        EngagementRate = eng,
                        MentionCount   = g.Count(),
                        EstimatedViews = avgViews * g.Count()
                    };
                })
                .OrderByDescending(c => c.EstimatedViews)
                .ToList();

            return new BrandAnalyticsDto
            {
                BrandName                 = brandName,
                TotalCreators             = grouped.Count,
                TotalVideos               = mentions.Count,
                EstimatedTotalViews       = Math.Round(grouped.Sum(c => c.EstimatedViews)),
                EstimatedTotalEngagement  = Math.Round(grouped.Sum(c =>
                    c.EstimatedViews * c.EngagementRate)),
                AverageConfidenceScore    = Math.Round(mentions.Average(m => m.ConfidenceScore), 3),
                Creators                  = grouped
            };
        }

        // ────────────────────────────────────────────────────────────────────
        // Detection logic
        // ────────────────────────────────────────────────────────────────────

        private record VideoMeta(string VideoId, string Title, string Description, List<string> Tags);
        private record Detection(string BrandName, string Method, double Confidence);

        private static List<Detection> Detect(string title, string description, List<string> tags)
        {
            var results = new List<Detection>();
            var combined  = $"{title} {description}";
            var allTags   = string.Join(" ", tags);

            // ── Tier 1: sponsor hashtags ─────────────────────────────────
            var allText = $"{combined} {allTags}".Split(new[] {' ', '\n', '\r', '\t'}, StringSplitOptions.RemoveEmptyEntries);
            foreach (var token in allText)
            {
                var t = token.TrimEnd('.', ',', '!', '?');
                if (SponsorHashtags.Contains(t))
                {
                    results.Add(new Detection(t.TrimStart('#').ToLowerInvariant(), "Hashtag", 0.95));
                }
            }

            // ── Tier 2: @-mention in description ────────────────────────
            foreach (Match m in MentionRegex.Matches(description))
            {
                var handle = m.Groups[1].Value;
                // Skip generic handles like "everyone", "me" etc.
                if (handle.Length >= 3 && !IsCommonWord(handle))
                    results.Add(new Detection($"@{handle.ToLowerInvariant()}", "Mention", 0.80));
            }

            // ── Tier 3: keyword phrases ──────────────────────────────────
            var descLower = description.ToLowerInvariant();
            foreach (var phrase in KeywordPhrases)
            {
                if (!descLower.Contains(phrase)) continue;

                // Try to extract what follows the phrase
                int idx = descLower.IndexOf(phrase, StringComparison.Ordinal);
                int afterIdx = idx + phrase.Length;
                string afterPhrase = description[afterIdx..].TrimStart(' ', '-', ':');
                // Take up to next whitespace or 30 chars
                var words = afterPhrase.Split([' ', '\n', '\r'], 2);
                string brandExtract = words.Length > 0 ? words[0].Trim('.', ',', '!', '?') : phrase;
                if (brandExtract.Length < 2 || IsCommonWord(brandExtract))
                    brandExtract = phrase; // fall back to the phrase itself

                results.Add(new Detection(brandExtract.ToLowerInvariant(), "Keyword", 0.70));
            }

            // Deduplicate: keep highest confidence per brand
            return results
                .GroupBy(d => d.BrandName)
                .Select(g => g.OrderByDescending(d => d.Confidence).First())
                .ToList();
        }

        private static readonly HashSet<string> CommonWords = new(StringComparer.OrdinalIgnoreCase)
            { "everyone", "me", "you", "us", "them", "all", "team", "here", "new", "my", "the", "this" };

        private static bool IsCommonWord(string s) => CommonWords.Contains(s);

        // ────────────────────────────────────────────────────────────────────
        // YouTube Data API calls
        // ────────────────────────────────────────────────────────────────────

        private async Task<List<VideoMeta>> FetchRecentVideosAsync(
            string channelId, int maxResults, CancellationToken ct)
        {
            if (string.IsNullOrEmpty(_apiKey)) return new List<VideoMeta>();

            try
            {
                using var http = _httpFactory.CreateClient();

                // ─ Step 1: PlaylistItems API (1 unit) to get recent video IDs ────────────
                // Uploads playlist ID = channel ID with 'UC' prefix swapped to 'UU'
                var uploadsId   = channelId.StartsWith("UC", StringComparison.Ordinal)
                                ? "UU" + channelId[2..] : channelId;
                var playlistUrl = $"https://www.googleapis.com/youtube/v3/playlistItems"
                                + $"?part=contentDetails&playlistId={uploadsId}&maxResults={maxResults}&key={_apiKey}";

                using var plResp = await http.GetAsync(playlistUrl, ct);
                if (!plResp.IsSuccessStatusCode)
                {
                    // 403/429 — propagate so the outer loop breaks immediately
                    if (plResp.StatusCode is System.Net.HttpStatusCode.Forbidden
                                           or System.Net.HttpStatusCode.TooManyRequests
                                           or System.Net.HttpStatusCode.Unauthorized)
                        throw new HttpRequestException($"YouTube API returned {(int)plResp.StatusCode}", null, plResp.StatusCode);
                    _logger.LogWarning("PlaylistItems API returned {S} for channel {C}", plResp.StatusCode, channelId);
                    return new List<VideoMeta>();
                }

                var plJson   = await plResp.Content.ReadFromJsonAsync<JsonDocument>(cancellationToken: ct);
                var plItems  = plJson?.RootElement.GetProperty("items");
                if (plItems is null) return new List<VideoMeta>();

                var videoIds = new List<string>();
                foreach (var item in plItems.Value.EnumerateArray())
                {
                    if (item.TryGetProperty("contentDetails", out var cd) &&
                        cd.TryGetProperty("videoId", out var vid))
                    {
                        var id = vid.GetString();
                        if (!string.IsNullOrEmpty(id)) videoIds.Add(id);
                    }
                }
                if (videoIds.Count == 0) return new List<VideoMeta>();

                // ─ Step 2: Videos API snippet (1 unit) — full title, description, tags ──
                var idList   = string.Join(",", videoIds.Take(50));
                var videoUrl = $"https://www.googleapis.com/youtube/v3/videos"
                            + $"?part=snippet&id={idList}&key={_apiKey}";

                using var vResp = await http.GetAsync(videoUrl, ct);
                if (!vResp.IsSuccessStatusCode) return new List<VideoMeta>();

                var vJson = await vResp.Content.ReadFromJsonAsync<JsonDocument>(cancellationToken: ct);
                if (vJson == null) return new List<VideoMeta>();

                var result = new List<VideoMeta>();
                foreach (var item in vJson.RootElement.GetProperty("items").EnumerateArray())
                {
                    string? id = item.GetProperty("id").GetString();
                    if (id == null) continue;

                    var snippet = item.GetProperty("snippet");
                    var title   = snippet.TryGetProperty("title",       out var t) ? t.GetString() ?? string.Empty : string.Empty;
                    var desc    = snippet.TryGetProperty("description", out var d) ? d.GetString() ?? string.Empty : string.Empty;
                    var tags    = new List<string>();
                    if (snippet.TryGetProperty("tags", out var tagsEl))
                        foreach (var tag in tagsEl.EnumerateArray())
                            if (tag.GetString() is { } s) tags.Add(s);

                    result.Add(new VideoMeta(id, title, desc, tags));
                }
                return result;
            }
            catch (OperationCanceledException) { throw; }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to fetch videos for channel {C}", channelId);
                return new List<VideoMeta>();
            }
        }

        private async Task<Dictionary<string, List<string>>> FetchVideoTagsAsync(
            List<string> videoIds, CancellationToken ct)
        {
            var result = new Dictionary<string, List<string>>();
            try
            {
                using var http = _httpFactory.CreateClient();
                var idList = string.Join(",", videoIds.Take(50));
                var url    = $"https://www.googleapis.com/youtube/v3/videos" +
                             $"?id={idList}&part=snippet&key={_apiKey}";

                using var resp = await http.GetAsync(url, ct);
                if (!resp.IsSuccessStatusCode) return result;

                var json = await resp.Content.ReadFromJsonAsync<JsonDocument>(cancellationToken: ct);
                if (json == null) return result;

                foreach (var item in json.RootElement.GetProperty("items").EnumerateArray())
                {
                    string? id = item.GetProperty("id").GetString();
                    if (id == null) continue;
                    var tags = new List<string>();
                    var snippet = item.GetProperty("snippet");
                    if (snippet.TryGetProperty("tags", out var tagsEl))
                        foreach (var t in tagsEl.EnumerateArray())
                            if (t.GetString() is { } s) tags.Add(s);
                    result[id] = tags;
                }
            }
            catch (Exception ex) { _logger.LogDebug(ex, "Tag fetch failed"); }
            return result;
        }

        private static double EstimateAvgViews(long totalViews, int videoCount) =>
            videoCount > 0 ? totalViews / (double)videoCount : 0;
    }
}
