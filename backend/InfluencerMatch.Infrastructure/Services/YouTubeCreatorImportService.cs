using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
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
    /// Comprehensive YouTube channel importer for SuperAdmin manual jobs.
    ///
    /// Per import run (20 channels, 10 videos each):
    ///   search.list          → 100 units (1 call)
    ///   channels.list batch  →   1 unit / channel  (~20)
    ///   playlistItems.list   →   1 unit / channel  (~20)
    ///   videos.list batch    →   1 unit / channel  (~20)
    ///   Total ≈ 160 units    → well within 10K/day free quota
    ///
    /// Stores per channel: thumbnail, banner, channel URL, published date,
    /// branding keywords, public email, Instagram/Twitter handles,
    /// recent videos (title, stats, tags, engagement), and pre-computed
    /// per-creator analytics aggregated from those videos.
    /// </summary>
    public sealed class YouTubeCreatorImportService : IYouTubeCreatorImportService
    {
        private readonly ApplicationDbContext _db;
        private readonly IHttpClientFactory   _http;
        private readonly IYouTubeQuotaTracker _quota;
        private readonly ILogger<YouTubeCreatorImportService> _log;
        private readonly string? _apiKey;

        // ──── Regex patterns ─────────────────────────────────────────────────
        private static readonly Regex EmailRx = new(
            @"\b[a-zA-Z0-9._%+\-]+@[a-zA-Z0-9.\-]+\.[a-zA-Z]{2,}\b",
            RegexOptions.Compiled | RegexOptions.IgnoreCase);

        // Use explicit platform signals only; plain "@word" catches email domains and creates false positives.
        private static readonly Regex InstagramUrlRx = new(
            @"(?:https?://)?(?:www\.)?instagram\.com/(?!p/|reel/|tv/|stories/)([a-zA-Z0-9._]{2,30})(?:[/?#]|$)",
            RegexOptions.Compiled | RegexOptions.IgnoreCase);
        private static readonly Regex InstagramLabeledRx = new(
            @"(?:instagram|insta|ig)\s*[:\-]?\s*@([a-zA-Z0-9._]{2,30})\b",
            RegexOptions.Compiled | RegexOptions.IgnoreCase);

        private static readonly Regex TwitterUrlRx = new(
            @"(?:https?://)?(?:www\.)?(?:twitter\.com|x\.com)/(?!home|explore|i/)([a-zA-Z0-9_]{1,15})(?:[/?#]|$)",
            RegexOptions.Compiled | RegexOptions.IgnoreCase);
        private static readonly Regex TwitterLabeledRx = new(
            @"(?:twitter|x)\s*[:\-]?\s*@([a-zA-Z0-9_]{1,15})\b",
            RegexOptions.Compiled | RegexOptions.IgnoreCase);

        public YouTubeCreatorImportService(
            ApplicationDbContext db,
            IHttpClientFactory   http,
            IYouTubeQuotaTracker quota,
            ILogger<YouTubeCreatorImportService> log,
            IConfiguration config)
        {
            _db     = db;
            _http   = http;
            _quota  = quota;
            _log    = log;
            _apiKey = config["YouTube:ApiKey"]
                   ?? config["YouTube__ApiKey"]
                   ?? config["YOUTUBE_API_KEY"];
        }

        public async Task<YouTubeImportResultDto> ImportAsync(
            YouTubeImportRequestDto request,
            CancellationToken ct = default)
        {
            if (string.IsNullOrWhiteSpace(_apiKey))
                throw new InvalidOperationException("YouTube:ApiKey is not configured.");

            var maxResults = Math.Clamp(request.MaxResults, 1, 50);
            var result     = new YouTubeImportResultDto { Timestamp = DateTime.UtcNow };

            // ── 1. Search for channels ─────────────────────────────────────────
            var channelIds = await SearchChannelIdsAsync(request.Query, maxResults, request.CountryCode, ct);
            if (channelIds.Count == 0)
            {
                _log.LogInformation("YouTube import: no channels for query='{Q}'", request.Query);
                return result;
            }

            // ── 2. Batch-fetch full channel details ────────────────────────────
            var channels = await FetchChannelDetailsAsync(channelIds, ct);

            // ── 3. For each channel: fetch videos + compute analytics + upsert ─
            foreach (var ch in channels)
            {
                ct.ThrowIfCancellationRequested();

                var maxVids = Math.Clamp(request.MaxVideosPerChannel, 3, 10);
                var videos  = await FetchRecentVideosAsync(ch, maxVids, ct);
                ComputeAnalytics(ch, videos);

                var row = new YouTubeImportResultRow
                {
                    ChannelId       = ch.ChannelId,
                    ChannelName     = ch.Title,
                    Subscribers     = ch.Subscribers,
                    TotalViews      = ch.TotalViews,
                    VideoCount      = ch.VideoCount,
                    Country         = ch.Country,
                    Category        = request.Category ?? ch.DefaultCategory,
                    Email           = ch.PublicEmail,
                    InstagramHandle = ch.InstagramHandle,
                    TwitterHandle   = ch.TwitterHandle,
                    ThumbnailUrl    = ch.ThumbnailUrl,
                    EngagementRate  = ch.EngagementRate,
                    AvgViews        = ch.AvgViews,
                };

                try
                {
                    var category = request.Category ?? ch.DefaultCategory ?? "General";
                    var existing = await _db.Creators
                        .FirstOrDefaultAsync(c => c.ChannelId == ch.ChannelId, ct);

                    Creator creator;
                    var isNew = false;
                    if (existing == null)
                    {
                        creator = new Creator { ChannelId = ch.ChannelId, CreatedAt = DateTime.UtcNow };
                        _db.Creators.Add(creator);
                        isNew = true;
                    }
                    else
                    {
                        creator = existing;
                    }

                    // ── Populate all fields ──────────────────────────────────
                    creator.ChannelName     = ch.Title;
                    creator.Description     = Trunc(ch.Description, 500);
                    creator.Subscribers     = ch.Subscribers;
                    creator.TotalViews      = ch.TotalViews;
                    creator.VideoCount      = ch.VideoCount;
                    creator.Country         = ch.Country ?? request.CountryCode ?? creator.Country;
                    creator.Category        = category;
                    creator.CreatorTier     = Creator.ComputeTier(ch.Subscribers);
                    creator.IsSmallCreator  = Creator.ComputeIsSmall(ch.Subscribers);
                    creator.ThumbnailUrl    = ch.ThumbnailUrl;
                    creator.BannerUrl       = ch.BannerUrl;
                    creator.ChannelUrl      = ch.ChannelUrl;
                    creator.PublishedAt     = ch.PublishedAt;
                    creator.ChannelTags     = ch.ChannelTags;
                    creator.PublicEmail     = ch.PublicEmail;
                    creator.InstagramHandle = ch.InstagramHandle;
                    creator.TwitterHandle   = ch.TwitterHandle;
                    creator.AvgViews        = ch.AvgViews;
                    creator.AvgLikes        = ch.AvgLikes;
                    creator.AvgComments     = ch.AvgComments;
                    creator.EngagementRate  = ch.EngagementRate;
                    creator.LastRefreshedAt = DateTime.UtcNow;
                    creator.UpdatedAt       = DateTime.UtcNow;

                    // Save now to get CreatorId for video FK
                    await _db.SaveChangesAsync(ct);

                    // ── Upsert recent videos ─────────────────────────────────
                    foreach (var v in videos)
                    {
                        var ev = await _db.Videos.FirstOrDefaultAsync(x => x.VideoId == v.VideoId, ct);
                        if (ev == null)
                        {
                            ev = new Video { VideoId = v.VideoId, CreatorId = creator.CreatorId };
                            _db.Videos.Add(ev);
                        }
                        ev.Title          = v.Title;
                        ev.ThumbnailUrl   = v.ThumbnailUrl;
                        ev.ViewCount      = v.ViewCount;
                        ev.LikeCount      = v.LikeCount;
                        ev.CommentCount   = v.CommentCount;
                        ev.PublishedAt    = v.PublishedAt;
                        ev.FetchedAt      = DateTime.UtcNow;
                        ev.Tags           = v.Tags;
                        ev.Description    = v.Description;
                        ev.EngagementRate = v.EngagementRate;
                    }

                    await _db.SaveChangesAsync(ct);

                    row.Status = isNew ? "new" : "updated";
                    if (isNew) result.Imported++;
                    else result.Updated++;
                }
                catch (Exception ex)
                {
                    _log.LogWarning(ex, "Import: failed for channel {Id}", ch.ChannelId);
                    row.Status = "skipped";
                    row.Error = Trunc(ex.InnerException?.Message ?? ex.Message, 220);
                    result.Skipped++;

                    // Prevent one failed entity state from cascading skips to all next rows.
                    _db.ChangeTracker.Clear();
                }

                result.Rows.Add(row);
            }

            result.QuotaUsed = _quota.UsedToday;
            return result;
        }

        // ── YouTube API calls ────────────────────────────────────────────────

        private async Task<List<string>> SearchChannelIdsAsync(
            string query, int max, string? regionCode, CancellationToken ct)
        {
            _quota.Consume(100);
            var url = "https://www.googleapis.com/youtube/v3/search"
                    + $"?part=id&type=channel&q={Uri.EscapeDataString(query)}&maxResults={max}"
                    + (string.IsNullOrWhiteSpace(regionCode) ? "" : $"&regionCode={Uri.EscapeDataString(regionCode)}")
                    + $"&key={_apiKey}";

            var body = await GetStringAsync(url, ct);
            if (body == null) return new();

            using var doc = JsonDocument.Parse(body);
            var ids = new List<string>();
            if (doc.RootElement.TryGetProperty("items", out var items))
                foreach (var item in items.EnumerateArray())
                    if (item.TryGetProperty("id", out var id) && id.TryGetProperty("channelId", out var cid))
                        ids.Add(cid.GetString() ?? "");

            return ids.Where(x => !string.IsNullOrWhiteSpace(x)).ToList();
        }

        private async Task<List<RichChannelDetail>> FetchChannelDetailsAsync(
            List<string> ids, CancellationToken ct)
        {
            _quota.Consume(ids.Count);
            var url = "https://www.googleapis.com/youtube/v3/channels"
                    + $"?part=id,snippet,statistics,brandingSettings,contentDetails"
                    + $"&id={string.Join(",", ids)}&maxResults=50&key={_apiKey}";

            var body = await GetStringAsync(url, ct);
            if (body == null) return new();

            using var doc = JsonDocument.Parse(body);
            if (!doc.RootElement.TryGetProperty("items", out var items)) return new();

            var results = new List<RichChannelDetail>();
            foreach (var item in items.EnumerateArray())
            {
                try  { results.Add(ParseChannelItem(item)); }
                catch (Exception ex) { _log.LogWarning(ex, "Failed to parse channel item"); }
            }
            return results;
        }

        private RichChannelDetail ParseChannelItem(JsonElement item)
        {
            var channelId = item.GetProperty("id").GetString() ?? "";
            var snippet   = item.GetProperty("snippet");

            var title       = GetStr(snippet, "title");
            var description = GetStr(snippet, "description");
            var country     = GetStrOpt(snippet, "country");
            var customUrl   = GetStrOpt(snippet, "customUrl");

            DateTime? publishedAt = null;
            if (snippet.TryGetProperty("publishedAt", out var pa) && DateTime.TryParse(pa.GetString(), out var pp))
                publishedAt = pp.ToUniversalTime();

            // Thumbnails
            string? thumbHigh = null;
            if (snippet.TryGetProperty("thumbnails", out var tn))
            {
                if (tn.TryGetProperty("high", out var hi) && hi.TryGetProperty("url", out var hu))      thumbHigh = hu.GetString();
                else if (tn.TryGetProperty("default", out var df) && df.TryGetProperty("url", out var du)) thumbHigh = du.GetString();
            }

            // Stats
            long subs = 0, views = 0; int vids = 0;
            if (item.TryGetProperty("statistics", out var st))
            {
                if (st.TryGetProperty("subscriberCount", out var s) && long.TryParse(s.GetString(), out var sv)) subs  = sv;
                if (st.TryGetProperty("viewCount",       out var v) && long.TryParse(v.GetString(), out var vv)) views = vv;
                if (st.TryGetProperty("videoCount",      out var c) && int.TryParse(c.GetString(),  out var cv)) vids  = cv;
            }

            // Branding: banner + keywords
            string? banner = null, keywords = null;
            if (item.TryGetProperty("brandingSettings", out var br))
            {
                if (br.TryGetProperty("image",   out var img) && img.TryGetProperty("bannerExternalUrl", out var bu)) banner   = bu.GetString();
                if (br.TryGetProperty("channel", out var bc)  && bc.TryGetProperty("keywords",           out var kw)) keywords = kw.GetString();
            }

            // Uploads playlist
            string? uploadsId = null;
            if (item.TryGetProperty("contentDetails", out var cd) &&
                cd.TryGetProperty("relatedPlaylists", out var rp) &&
                rp.TryGetProperty("uploads", out var up))
                uploadsId = up.GetString();

            var (email, ig, tw) = ParseContactInfo(description);

            return new RichChannelDetail
            {
                ChannelId         = channelId,
                Title             = title,
                Description       = description,
                Country           = country,
                ThumbnailUrl      = thumbHigh,
                BannerUrl         = banner,
                ChannelUrl        = !string.IsNullOrWhiteSpace(customUrl)
                                        ? $"https://youtube.com/{customUrl}"
                                        : $"https://youtube.com/channel/{channelId}",
                PublishedAt       = publishedAt,
                ChannelTags       = Trunc(keywords, 500),
                Subscribers       = subs,
                TotalViews        = views,
                VideoCount        = vids,
                DefaultCategory   = InferCategory((keywords ?? "") + " " + description),
                PublicEmail       = email,
                InstagramHandle   = ig,
                TwitterHandle     = tw,
                UploadsPlaylistId = uploadsId,
            };
        }

        private async Task<List<VideoDetail>> FetchRecentVideosAsync(
            RichChannelDetail ch, int max, CancellationToken ct)
        {
            if (string.IsNullOrWhiteSpace(ch.UploadsPlaylistId)) return new();

            // playlistItems.list → 1 unit
            _quota.Consume(1);
            var plUrl = "https://www.googleapis.com/youtube/v3/playlistItems"
                      + $"?part=contentDetails&playlistId={Uri.EscapeDataString(ch.UploadsPlaylistId)}"
                      + $"&maxResults={max}&key={_apiKey}";

            var plBody = await GetStringAsync(plUrl, ct);
            if (plBody == null) return new();

            var videoIds = new List<string>();
            using (var doc = JsonDocument.Parse(plBody))
                if (doc.RootElement.TryGetProperty("items", out var items))
                    foreach (var it in items.EnumerateArray())
                        if (it.TryGetProperty("contentDetails", out var cd) && cd.TryGetProperty("videoId", out var vi))
                            videoIds.Add(vi.GetString() ?? "");

            videoIds = videoIds.Where(x => !string.IsNullOrWhiteSpace(x)).ToList();
            if (videoIds.Count == 0) return new();

            // videos.list → 1 unit
            _quota.Consume(1);
            var vUrl = "https://www.googleapis.com/youtube/v3/videos"
                     + $"?part=id,snippet,statistics&id={string.Join(",", videoIds)}&key={_apiKey}";

            var vBody = await GetStringAsync(vUrl, ct);
            if (vBody == null) return new();

            var results = new List<VideoDetail>();
            using (var doc = JsonDocument.Parse(vBody))
            {
                if (!doc.RootElement.TryGetProperty("items", out var items)) return results;
                foreach (var item in items.EnumerateArray())
                {
                    try
                    {
                        var vid     = item.GetProperty("id").GetString() ?? "";
                        var snippet = item.GetProperty("snippet");
                        var title   = GetStr(snippet, "title");
                        var desc    = Trunc(GetStrOpt(snippet, "description"), 300);

                        string? thumb = null;
                        if (snippet.TryGetProperty("thumbnails", out var tn) &&
                            tn.TryGetProperty("medium", out var mt) && mt.TryGetProperty("url", out var mu))
                            thumb = mu.GetString();

                        DateTime pubAt = DateTime.UtcNow;
                        if (snippet.TryGetProperty("publishedAt", out var pa) && DateTime.TryParse(pa.GetString(), out var pv))
                            pubAt = pv.ToUniversalTime();

                        string? tags = null;
                        if (snippet.TryGetProperty("tags", out var tagEl))
                        {
                            var tl = new List<string>();
                            foreach (var t in tagEl.EnumerateArray()) tl.Add(t.GetString() ?? "");
                            tags = Trunc(string.Join(",", tl.Take(20)), 500);
                        }

                        long vc = 0, lc = 0, cc = 0;
                        if (item.TryGetProperty("statistics", out var st))
                        {
                            if (st.TryGetProperty("viewCount",    out var v) && long.TryParse(v.GetString(), out var vv)) vc = vv;
                            if (st.TryGetProperty("likeCount",    out var l) && long.TryParse(l.GetString(), out var lv)) lc = lv;
                            if (st.TryGetProperty("commentCount", out var c) && long.TryParse(c.GetString(), out var cv)) cc = cv;
                        }

                        results.Add(new VideoDetail
                        {
                            VideoId        = vid,
                            Title          = title,
                            Description    = desc,
                            ThumbnailUrl   = thumb,
                            Tags           = tags,
                            ViewCount      = vc,
                            LikeCount      = lc,
                            CommentCount   = cc,
                            PublishedAt    = pubAt,
                            EngagementRate = vc > 0 ? Math.Round((double)(lc + cc) / vc * 100, 2) : 0,
                        });
                    }
                    catch (Exception ex) { _log.LogWarning(ex, "Failed to parse video item"); }
                }
            }
            return results;
        }

        // ── Analytics ────────────────────────────────────────────────────────

        private static void ComputeAnalytics(RichChannelDetail ch, List<VideoDetail> videos)
        {
            if (videos.Count == 0) return;
            ch.AvgViews    = Math.Round(videos.Average(v => (double)v.ViewCount),    0);
            ch.AvgLikes    = Math.Round(videos.Average(v => (double)v.LikeCount),    0);
            ch.AvgComments = Math.Round(videos.Average(v => (double)v.CommentCount), 0);
            ch.EngagementRate = ch.AvgViews > 0
                ? Math.Round((ch.AvgLikes + ch.AvgComments) / ch.AvgViews * 100, 2)
                : 0;
        }

        // ── Contact parsing ──────────────────────────────────────────────────

        private static (string? email, string? instagram, string? twitter) ParseContactInfo(string text)
        {
            if (string.IsNullOrWhiteSpace(text)) return (null, null, null);

            string? email = null;
            var em = EmailRx.Match(text);
            if (em.Success)
            {
                var c = em.Value.ToLowerInvariant();
                if (!c.Contains("youtube.com") && !c.Contains("google.com") && !c.Contains("example.com"))
                    email = c;
            }

            var ig = FirstValidHandle(text, IsValidInstagramHandle, InstagramUrlRx, InstagramLabeledRx);
            var tw = FirstValidHandle(text, IsValidTwitterHandle, TwitterUrlRx, TwitterLabeledRx);

            return (email, ig, tw);
        }

        private static string? FirstValidHandle(string text, Func<string, bool> isValid, params Regex[] patterns)
        {
            foreach (var pattern in patterns)
            {
                var m = pattern.Match(text);
                if (!m.Success) continue;

                var handle = m.Groups[1].Value.Trim();
                if (isValid(handle))
                    return handle;
            }

            return null;
        }

        private static bool IsValidInstagramHandle(string? handle)
        {
            if (string.IsNullOrWhiteSpace(handle)) return false;

            var h = handle.Trim().TrimStart('@').ToLowerInvariant();
            if (h.Length < 2 || h.Length > 30) return false;
            if (!Regex.IsMatch(h, @"^[a-z0-9._]+$")) return false;
            if (h.StartsWith('.') || h.EndsWith('.')) return false;

            // Reject common email-domain tokens that can appear after '@' in emails.
            if (h == "gmail" || h == "yahoo" || h == "hotmail" || h == "outlook" || h == "protonmail") return false;
            if (h.EndsWith(".com") || h.EndsWith(".net") || h.EndsWith(".org") || h.EndsWith(".in") || h.EndsWith(".co")) return false;

            return true;
        }

        private static bool IsValidTwitterHandle(string? handle)
        {
            if (string.IsNullOrWhiteSpace(handle)) return false;

            var h = handle.Trim().TrimStart('@').ToLowerInvariant();
            if (h.Length < 2 || h.Length > 15) return false;
            if (!Regex.IsMatch(h, @"^[a-z0-9_]+$")) return false;

            return true;
        }

        // ── Internal helpers ─────────────────────────────────────────────────

        private async Task<string?> GetStringAsync(string url, CancellationToken ct)
        {
            try
            {
                var client = _http.CreateClient();
                using var resp = await client.GetAsync(url, ct);
                if (!resp.IsSuccessStatusCode)
                {
                    var err = await resp.Content.ReadAsStringAsync(ct);
                    _log.LogWarning("YouTube API HTTP {Status}: {Body}",
                        (int)resp.StatusCode, err.Length > 400 ? err[..400] : err);
                    return null;
                }
                return await resp.Content.ReadAsStringAsync(ct);
            }
            catch (Exception ex)
            {
                _log.LogWarning(ex, "HTTP GET failed");
                return null;
            }
        }

        private static string  GetStr(JsonElement el, string key)    => el.TryGetProperty(key, out var v) ? v.GetString() ?? "" : "";
        private static string? GetStrOpt(JsonElement el, string key) => el.TryGetProperty(key, out var v) ? v.GetString() : null;
        private static string? Trunc(string? s, int max)             => s == null ? null : s.Length <= max ? s : s[..max];

        private static string? InferCategory(string text)
        {
            if (string.IsNullOrWhiteSpace(text)) return null;
            var t = text.ToLowerInvariant();
            if (t.Contains("cook") || t.Contains("food") || t.Contains("recipe"))       return "Food";
            if (t.Contains("tech") || t.Contains("gadget") || t.Contains("phone"))      return "Tech";
            if (t.Contains("fashion") || t.Contains("style") || t.Contains("beauty"))   return "Fashion";
            if (t.Contains("fitness") || t.Contains("gym") || t.Contains("health"))     return "Fitness";
            if (t.Contains("travel") || t.Contains("vlog"))                              return "Travel";
            if (t.Contains("gaming") || t.Contains("game"))                              return "Gaming";
            if (t.Contains("educat") || t.Contains("study") || t.Contains("learn"))     return "Education";
            if (t.Contains("comedy") || t.Contains("funny") || t.Contains("entertain")) return "Entertainment";
            if (t.Contains("music") || t.Contains("song"))                               return "Music";
            if (t.Contains("business") || t.Contains("finance") || t.Contains("money")) return "Finance";
            return null;
        }

        // ── Inner data transfer classes ──────────────────────────────────────

        private sealed class RichChannelDetail
        {
            public string    ChannelId         { get; set; } = string.Empty;
            public string    Title             { get; set; } = string.Empty;
            public string?   Description       { get; set; }
            public string?   Country           { get; set; }
            public string?   ThumbnailUrl      { get; set; }
            public string?   BannerUrl         { get; set; }
            public string?   ChannelUrl        { get; set; }
            public DateTime? PublishedAt       { get; set; }
            public string?   ChannelTags       { get; set; }
            public long      Subscribers       { get; set; }
            public long      TotalViews        { get; set; }
            public int       VideoCount        { get; set; }
            public string?   DefaultCategory   { get; set; }
            public string?   PublicEmail       { get; set; }
            public string?   InstagramHandle   { get; set; }
            public string?   TwitterHandle     { get; set; }
            public string?   UploadsPlaylistId { get; set; }
            // filled by ComputeAnalytics:
            public double    AvgViews          { get; set; }
            public double    AvgLikes          { get; set; }
            public double    AvgComments       { get; set; }
            public double    EngagementRate    { get; set; }
        }

        private sealed class VideoDetail
        {
            public string    VideoId        { get; set; } = string.Empty;
            public string    Title          { get; set; } = string.Empty;
            public string?   Description    { get; set; }
            public string?   ThumbnailUrl   { get; set; }
            public string?   Tags           { get; set; }
            public long      ViewCount      { get; set; }
            public long      LikeCount      { get; set; }
            public long      CommentCount   { get; set; }
            public DateTime  PublishedAt    { get; set; }
            public double    EngagementRate { get; set; }
        }
    }
}
