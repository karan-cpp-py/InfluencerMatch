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
    /// <summary>
    /// Fetches recent videos for each creator, computes engagement rates,
    /// detects brand collaborations by matching against a curated brand dictionary,
    /// and upserts per-video records into the <c>VideoAnalytics</c> table.
    ///
    /// YouTube API quota cost per creator:
    ///   • 1 unit  — PlaylistItems list (video IDs)
    ///   • 1 unit  — Videos list (snippet + statistics for up to 50 videos)
    /// </summary>
    public class VideoAnalyticsService : IVideoAnalyticsService
    {
        // ── Brand Dictionary ─────────────────────────────────────────────────
        // Matched case-insensitively as whole-word tokens in title + description + tags.
        private static readonly string[] KnownBrands =
        {
            // Tech
            "samsung", "apple", "oneplus", "oppo", "vivo", "realme", "motorola", "nokia",
            "mi", "xiaomi", "redmi", "sony", "lg", "huawei", "google", "asus", "lenovo",
            "hp", "dell", "intel", "amd", "nvidia", "jbl", "bose", "sennheiser",
            // Indian consumer electronics / audio
            "boat", "noise", "ptron", "boult", "mivi", "crossbeats", "zebronics",
            // Fashion / Sportswear
            "nike", "adidas", "puma", "reebok", "under armour", "zara", "h&m",
            "levis", "wrangler", "pepe jeans", "bewakoof", "myntra", "ajio",
            // E-commerce
            "amazon", "flipkart", "meesho", "snapdeal", "nykaa",
            // Food & Delivery
            "swiggy", "zomato", "mcdonald", "kfc", "dominos", "subway", "maggi", "haldirams",
            // Beauty & Personal Care
            "mamaearth", "mcaffeine", "plum", "dot & key", "minimalist", "lakme",
            "loreal", "garnier", "himalaya", "nivea", "dove", "gillette",
            // Finance / Banking
            "paytm", "phonepe", "gpay", "cred", "groww", "zerodha", "angelone",
            "hdfc", "icici", "sbi",
            // Travel / Hospitality
            "makemytrip", "goibibo", "oyo", "airbnb",
            // Automotive
            "royal enfield", "hero", "honda", "bajaj", "tvs", "yamaha", "suzuki",
            // EdTech
            "byju", "unacademy", "vedantu", "upgrad", "coursera",
            // Gaming
            "razer", "corsair", "logitech",
        };

        // Pre-compiled word-boundary patterns for each brand
        private static readonly (string Brand, Regex Pattern)[] BrandPatterns =
            KnownBrands
                .Select(b => (b, new Regex(
                    @"\b" + Regex.Escape(b) + @"\b",
                    RegexOptions.Compiled | RegexOptions.IgnoreCase)))
                .ToArray();

        // ── Fields ───────────────────────────────────────────────────────────
        private readonly ApplicationDbContext _db;
        private readonly IHttpClientFactory   _http;
        private readonly ILogger<VideoAnalyticsService> _log;
        private readonly string? _apiKey;

        public VideoAnalyticsService(
            ApplicationDbContext db,
            IHttpClientFactory   http,
            ILogger<VideoAnalyticsService> log,
            IConfiguration config)
        {
            _db     = db;
            _http   = http;
            _log    = log;
            _apiKey = config["YouTube:ApiKey"];
        }

        // ── Public API ────────────────────────────────────────────────────────

        public async Task<int> RefreshCreatorAsync(int creatorId, CancellationToken ct = default)
        {
            var creator = await _db.Creators.AsNoTracking()
                .FirstOrDefaultAsync(c => c.CreatorId == creatorId, ct);
            if (creator == null) return 0;

            var videos = await FetchRecentVideosAsync(creator.ChannelId, 10, ct);
            if (videos.Count == 0) return 0;

            var now = DateTime.UtcNow;
            int upserted = 0;

            foreach (var v in videos)
            {
                ct.ThrowIfCancellationRequested();

                var engRate = v.Views > 0
                    ? Math.Round((double)(v.Likes + v.Comments) / v.Views * 100.0, 4)
                    : 0.0;

                var brand = DetectBrand(v.Title, v.Description);
                var videoType = brand != null ? "Sponsored" : "Organic";

                var existing = await _db.VideoAnalytics
                    .FirstOrDefaultAsync(va => va.YoutubeVideoId == v.VideoId, ct);

                if (existing == null)
                {
                    _db.VideoAnalytics.Add(new VideoAnalytics
                    {
                        YoutubeVideoId = v.VideoId,
                        CreatorId      = creatorId,
                        Title          = v.Title,
                        Views          = v.Views,
                        Likes          = v.Likes,
                        Comments       = v.Comments,
                        EngagementRate = engRate,
                        BrandName      = brand,
                        VideoType      = videoType,
                        PublishedAt    = v.PublishedAt,
                        RecordedAt     = now
                    });
                }
                else
                {
                    // Update metrics; re-run brand detection for latest title/description
                    existing.Views          = v.Views;
                    existing.Likes          = v.Likes;
                    existing.Comments       = v.Comments;
                    existing.EngagementRate = engRate;
                    existing.BrandName      = brand;
                    existing.VideoType      = videoType;
                    existing.RecordedAt     = now;
                }

                upserted++;
            }

            await _db.SaveChangesAsync(ct);
            _log.LogInformation(
                "VideoAnalytics: creator {Id} ({Name}) — {N} videos upserted",
                creatorId, creator.ChannelName, upserted);
            return upserted;
        }

        public async Task RefreshAllAsync(CancellationToken ct = default)
        {
            var ids = await _db.Creators.Where(c => c.UserId != null).Select(c => c.CreatorId).ToListAsync(ct);
            _log.LogInformation("VideoAnalytics refresh starting for {N} creators", ids.Count);

            int total = 0;
            foreach (var id in ids)
            {
                if (ct.IsCancellationRequested) break;
                try
                {
                    total += await RefreshCreatorAsync(id, ct);
                }
                catch (HttpRequestException httpEx) when (
                    httpEx.StatusCode is System.Net.HttpStatusCode.Forbidden
                                       or System.Net.HttpStatusCode.TooManyRequests
                                       or System.Net.HttpStatusCode.Unauthorized)
                {
                    _log.LogWarning(
                        "VideoAnalytics: YouTube API returned {Code} — stopping batch.",
                        (int?)httpEx.StatusCode);
                    break;
                }
                catch (OperationCanceledException) { break; }
                catch (Exception ex)
                {
                    _log.LogWarning(ex, "VideoAnalytics: failed for creator {Id}", id);
                }

                await Task.Delay(300, ct);
            }

            _log.LogInformation("VideoAnalytics refresh complete. {N} rows upserted.", total);
        }

        public async Task<CreatorVideoAnalyticsSummaryDto?> GetCreatorSummaryAsync(int creatorId)
        {
            var creator = await _db.Creators.AsNoTracking()
                .FirstOrDefaultAsync(c => c.CreatorId == creatorId);
            if (creator == null) return null;

            var rows = await _db.VideoAnalytics.AsNoTracking()
                .Where(v => v.CreatorId == creatorId)
                .OrderByDescending(v => v.PublishedAt)
                .ToListAsync();

            var organic   = rows.Where(v => v.VideoType == "Organic").ToList();
            var sponsored = rows.Where(v => v.VideoType == "Sponsored").ToList();

            return new CreatorVideoAnalyticsSummaryDto
            {
                CreatorId         = creatorId,
                ChannelName       = creator.ChannelName,
                TotalVideos       = rows.Count,
                AvgViews          = rows.Count > 0 ? Math.Round(rows.Average(v => (double)v.Views), 1) : 0,
                AvgEngagementRate = rows.Count > 0 ? Math.Round(rows.Average(v => v.EngagementRate), 3) : 0,

                OrganicVideos     = organic.Count,
                AvgOrganicViews   = organic.Count > 0 ? Math.Round(organic.Average(v => (double)v.Views), 1) : 0,
                AvgOrganicEng     = organic.Count > 0 ? Math.Round(organic.Average(v => v.EngagementRate), 3) : 0,

                SponsoredVideos   = sponsored.Count,
                AvgSponsoredViews = sponsored.Count > 0 ? Math.Round(sponsored.Average(v => (double)v.Views), 1) : 0,
                AvgSponsoredEng   = sponsored.Count > 0 ? Math.Round(sponsored.Average(v => v.EngagementRate), 3) : 0,

                DetectedBrands    = sponsored
                    .Where(v => v.BrandName != null)
                    .Select(v => v.BrandName!)
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .OrderBy(b => b)
                    .ToList(),

                Videos = rows.Select(v => new VideoAnalyticsItemDto
                {
                    YoutubeVideoId = v.YoutubeVideoId,
                    Title          = v.Title,
                    Views          = v.Views,
                    Likes          = v.Likes,
                    Comments       = v.Comments,
                    EngagementRate = v.EngagementRate,
                    BrandName      = v.BrandName,
                    VideoType      = v.VideoType,
                    PublishedAt    = v.PublishedAt
                }).ToList()
            };
        }

        public async Task<BrandCreatorStatsDto> GetBrandCreatorsAsync(string brandName)
        {
            var lower = brandName.ToLowerInvariant().Trim();

            var rows = await _db.VideoAnalytics.AsNoTracking()
                .Include(v => v.Creator)
                .Where(v => v.BrandName != null && v.BrandName.ToLower().Contains(lower))
                .ToListAsync();

            var grouped = rows
                .GroupBy(v => v.CreatorId)
                .Select(g =>
                {
                    var creator = g.First().Creator;
                    return new BrandCreatorEntryDto
                    {
                        CreatorId     = g.Key,
                        ChannelName   = creator.ChannelName,
                        Subscribers   = creator.Subscribers,
                        Category      = creator.Category ?? string.Empty,
                        VideoCount    = g.Count(),
                        TotalViews    = g.Sum(v => (double)v.Views),
                        AvgEngagement = g.Count() > 0
                            ? Math.Round(g.Average(v => v.EngagementRate), 3)
                            : 0,
                        LastDetectedAt = g.Max(v => v.RecordedAt)
                    };
                })
                .OrderByDescending(c => c.TotalViews)
                .ToList();

            return new BrandCreatorStatsDto
            {
                BrandName     = brandName,
                TotalCreators = grouped.Count,
                TotalVideos   = rows.Count,
                TotalViews    = grouped.Sum(c => c.TotalViews),
                AvgEngagement = grouped.Count > 0
                    ? Math.Round(grouped.Average(c => c.AvgEngagement), 3)
                    : 0,
                Creators      = grouped
            };
        }

        // ── YouTube API ───────────────────────────────────────────────────────

        private record VideoMeta(
            string VideoId,
            string Title,
            string Description,
            long   Views,
            long   Likes,
            long   Comments,
            DateTime PublishedAt);

        private async Task<List<VideoMeta>> FetchRecentVideosAsync(
            string channelId, int maxResults, CancellationToken ct)
        {
            if (string.IsNullOrEmpty(_apiKey))
            {
                _log.LogWarning("VideoAnalytics: YouTube API key not configured.");
                return new List<VideoMeta>();
            }

            using var http = _http.CreateClient();

            // ── Step 1: PlaylistItems API — get video IDs ─────────────────────
            var uploadsId = channelId.StartsWith("UC", StringComparison.Ordinal)
                ? "UU" + channelId[2..]
                : channelId;

            var playlistUrl =
                $"https://www.googleapis.com/youtube/v3/playlistItems" +
                $"?part=contentDetails" +
                $"&playlistId={Uri.EscapeDataString(uploadsId)}" +
                $"&maxResults={maxResults}" +
                $"&key={_apiKey}";

            var playlistResp = await http.GetAsync(playlistUrl, ct);

            if (!playlistResp.IsSuccessStatusCode)
            {
                var body = await playlistResp.Content.ReadAsStringAsync(ct);
                _log.LogWarning(
                    "VideoAnalytics: PlaylistItems returned {Code}: {Body}",
                    (int)playlistResp.StatusCode, body[..Math.Min(body.Length, 300)]);
                playlistResp.EnsureSuccessStatusCode(); // propagates 403 → breaks batch
            }

            var playlistDoc = await playlistResp.Content.ReadFromJsonAsync<
                System.Text.Json.JsonElement>(cancellationToken: ct);

            var videoIds = new List<string>();
            if (playlistDoc.TryGetProperty("items", out var items))
            {
                foreach (var item in items.EnumerateArray())
                {
                    if (item.TryGetProperty("contentDetails", out var cd) &&
                        cd.TryGetProperty("videoId", out var vid))
                        videoIds.Add(vid.GetString() ?? string.Empty);
                }
            }

            if (videoIds.Count == 0) return new List<VideoMeta>();

            // ── Step 2: Videos API — get snippet + statistics ─────────────────
            var idsParam = Uri.EscapeDataString(string.Join(",", videoIds));
            var videosUrl =
                $"https://www.googleapis.com/youtube/v3/videos" +
                $"?part=snippet,statistics" +
                $"&id={idsParam}" +
                $"&key={_apiKey}";

            var videosResp = await http.GetAsync(videosUrl, ct);

            if (!videosResp.IsSuccessStatusCode)
            {
                var body = await videosResp.Content.ReadAsStringAsync(ct);
                _log.LogWarning(
                    "VideoAnalytics: Videos API returned {Code}: {Body}",
                    (int)videosResp.StatusCode, body[..Math.Min(body.Length, 300)]);
                videosResp.EnsureSuccessStatusCode();
            }

            var videosDoc = await videosResp.Content.ReadFromJsonAsync<
                System.Text.Json.JsonElement>(cancellationToken: ct);

            var result = new List<VideoMeta>();

            if (videosDoc.TryGetProperty("items", out var vItems))
            {
                foreach (var vItem in vItems.EnumerateArray())
                {
                    var id          = vItem.GetProperty("id").GetString() ?? string.Empty;
                    var snippet     = vItem.GetProperty("snippet");
                    var stats       = vItem.GetProperty("statistics");
                    var title       = snippet.TryGetProperty("title", out var t) ? t.GetString() ?? "" : "";
                    var description = snippet.TryGetProperty("description", out var d) ? d.GetString() ?? "" : "";
                    var publishedAt = snippet.TryGetProperty("publishedAt", out var pa)
                        ? DateTime.Parse(pa.GetString() ?? DateTime.UtcNow.ToString("o"))
                        : DateTime.UtcNow;

                    long views    = ParseLong(stats, "viewCount");
                    long likes    = ParseLong(stats, "likeCount");
                    long comments = ParseLong(stats, "commentCount");

                    result.Add(new VideoMeta(id, title, description, views, likes, comments, publishedAt));
                }
            }

            return result;
        }

        // ── Brand detection ───────────────────────────────────────────────────

        private static string? DetectBrand(string title, string description)
        {
            var searchText = $"{title} {description}";
            foreach (var (brand, pattern) in BrandPatterns)
            {
                if (pattern.IsMatch(searchText))
                    return brand;
            }
            return null;
        }

        private static long ParseLong(System.Text.Json.JsonElement stats, string key)
        {
            if (stats.TryGetProperty(key, out var prop) &&
                long.TryParse(prop.GetString(), out var val))
                return val;
            return 0;
        }
    }
}
