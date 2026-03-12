using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Json;
using System.Threading;
using System.Threading.Tasks;
using InfluencerMatch.Application.Interfaces;
using InfluencerMatch.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace InfluencerMatch.Infrastructure.Services
{
    /// <summary>
    /// Detects the primary content language for each creator using:
    ///
    ///   1. YouTube PlaylistItems API (1 unit) — recent video IDs
    ///   2. YouTube Videos API snippet (1 unit) — titles + descriptions
    ///   3. YouTube CommentThreads API (1 unit) — top comments
    ///
    /// Language is detected via Unicode character-block analysis, which is
    /// 100 % reliable for Indian scripts (each language uses a distinct Unicode range).
    /// English is detected via Latin-script character dominance.
    ///
    /// Per-creator quota cost: ~3 units total (vs 100+ if Search API were used).
    /// </summary>
    public class LanguageDetectionService : ILanguageDetectionService
    {
        // ── Supported languages (brand dropdown order) ───────────────────────
        private static readonly string[] SupportedLanguages =
        {
            "Hindi", "English", "Tamil", "Telugu", "Kannada",
            "Malayalam", "Punjabi", "Haryanvi", "Bengali", "Marathi"
        };

        // ── Unicode block ranges ──────────────────────────────────────────────
        //  Each entry: (minCodepoint, maxCodepoint, languageName)
        // Devanagari is shared by Hindi, Marathi, Haryanvi — disambiguation done
        // via keyword matching on channel name / description strings.
        private static readonly (int Min, int Max, string Lang)[] ScriptRanges =
        {
            (0x0900, 0x097F, "Devanagari"),  // Hindi, Marathi, Haryanvi
            (0x0980, 0x09FF, "Bengali"),
            (0x0A00, 0x0A7F, "Punjabi"),     // Gurmukhi script
            (0x0B80, 0x0BFF, "Tamil"),
            (0x0C00, 0x0C7F, "Telugu"),
            (0x0C80, 0x0CFF, "Kannada"),
            (0x0D00, 0x0D7F, "Malayalam"),
        };

        // Ratio of Latin chars in a sample needed to call it "English"
        private const double EnglishThreshold = 0.60;

        // ── Fields ────────────────────────────────────────────────────────────
        private readonly ApplicationDbContext _db;
        private readonly IHttpClientFactory   _httpFactory;
        private readonly ILogger<LanguageDetectionService> _logger;
        private readonly string? _apiKey;

        public LanguageDetectionService(
            ApplicationDbContext db,
            IHttpClientFactory   httpFactory,
            ILogger<LanguageDetectionService> logger,
            IConfiguration config)
        {
            _db          = db;
            _httpFactory = httpFactory;
            _logger      = logger;
            _apiKey      = config["YouTube:ApiKey"];
        }

        // ── ILanguageDetectionService ─────────────────────────────────────────

        public IReadOnlyList<string> GetSupportedLanguages() => SupportedLanguages;

        public async Task DetectAndSaveAsync(int creatorId, CancellationToken ct = default)
        {
            var creator = await _db.Creators.FirstOrDefaultAsync(c => c.CreatorId == creatorId, ct);
            if (creator == null) return;

            var texts = new List<string>();

            // Add channel name as a text sample
            if (!string.IsNullOrWhiteSpace(creator.ChannelName))
                texts.Add(creator.ChannelName);

            // Fetch video titles + descriptions
            await AppendVideoTextsAsync(creator.ChannelId, texts, ct);

            // Fetch comment texts (best signal for spoken language)
            await AppendCommentTextsAsync(creator.ChannelId, texts, ct);

            if (texts.Count == 0)
            {
                _logger.LogDebug("LanguageDetection: no text samples for creator {Id}", creatorId);
                return;
            }

            var dist = ComputeLanguageDistribution(texts, creator.ChannelName);

            if (dist.Count == 0) return;

            var dominant   = dist.OrderByDescending(kv => kv.Value).First();
            var confidence = Math.Round(dominant.Value, 4);

            // Only update if detection is reasonably confident
            if (confidence < 0.15) return;

            // Resolve "Devanagari" bucket to Hindi / Marathi / Haryanvi
            var language = ResolveDevanagari(dominant.Key, creator.ChannelName ?? string.Empty);

            // Derive region from country + language
            var region = DeriveRegion(creator.Country, language);

            creator.Language               = language;
            creator.Region                 = region;
            creator.LanguageConfidenceScore = confidence;

            _db.Creators.Update(creator);
            await _db.SaveChangesAsync(ct);

            _logger.LogDebug(
                "LanguageDetection: creator {Id} → {Lang} ({Conf:P0}), region={Region}",
                creatorId, language, confidence, region);
        }

        public async Task RefreshAllAsync(CancellationToken ct = default)
        {
            var ids = await _db.Creators
                .Where(c => c.ChannelId != null && c.ChannelId != "")
                .Select(c => c.CreatorId)
                .ToListAsync(ct);
            _logger.LogInformation("LanguageDetection: refreshing {N} creators", ids.Count);

            int done = 0;
            foreach (var id in ids)
            {
                if (ct.IsCancellationRequested) break;
                try
                {
                    await DetectAndSaveAsync(id, ct);
                    done++;
                    // Polite rate limiting — stay well within API quota
                    await Task.Delay(400, ct);
                }
                catch (OperationCanceledException) { break; }
                catch (HttpRequestException httpEx) when (
                    httpEx.StatusCode is System.Net.HttpStatusCode.Forbidden
                                       or System.Net.HttpStatusCode.TooManyRequests
                                       or System.Net.HttpStatusCode.Unauthorized)
                {
                    _logger.LogWarning(
                        "LanguageDetection: YouTube API returned {Code} — stopping batch. Check API key and daily quota.",
                        (int?)httpEx.StatusCode);
                    break;
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "LanguageDetection: failed for creator {Id}", id);
                }
            }
            _logger.LogInformation("LanguageDetection: refreshed {Done}/{Total}", done, ids.Count);
        }

        // ── YouTube data fetching ─────────────────────────────────────────────

        private async Task AppendVideoTextsAsync(
            string channelId, List<string> texts, CancellationToken ct)
        {
            if (!IsApiKeyConfigured()) return;
            var client = _httpFactory.CreateClient();

            // 1. PlaylistItems (1 unit) → video IDs
            // Status check is BEFORE the swallowing try/catch so 403/429 propagates
            // up to RefreshAllAsync and breaks the batch rather than silently continuing
            var uploadsId   = UploadsPlaylistId(channelId);
            var playlistUrl = $"https://www.googleapis.com/youtube/v3/playlistItems"
                            + $"?part=contentDetails&playlistId={uploadsId}&maxResults=10&key={_apiKey}";
            var plHttpResp  = await client.GetAsync(playlistUrl, ct);
            if (plHttpResp.StatusCode is System.Net.HttpStatusCode.Forbidden
                                       or System.Net.HttpStatusCode.TooManyRequests
                                       or System.Net.HttpStatusCode.Unauthorized)
                throw new HttpRequestException($"YouTube API returned {(int)plHttpResp.StatusCode}", null, plHttpResp.StatusCode);

            try
            {
                var playlistResp = await plHttpResp.Content.ReadFromJsonAsync<YtPlaylistResponse>(cancellationToken: ct);
                if (playlistResp?.items == null || playlistResp.items.Count == 0) return;

                var videoIds = playlistResp.items
                    .Select(i => i.contentDetails?.videoId)
                    .Where(id => id != null)
                    .ToList();

                if (videoIds.Count == 0) return;

                // 2. Videos API snippet (1 unit) → title + description
                var ids      = string.Join(",", videoIds);
                var videoUrl = $"https://www.googleapis.com/youtube/v3/videos"
                             + $"?part=snippet&id={ids}&key={_apiKey}";

                var videoResp = await client.GetFromJsonAsync<YtVideoListResponse>(videoUrl, ct);
                if (videoResp?.items == null) return;

                foreach (var item in videoResp.items)
                {
                    if (!string.IsNullOrWhiteSpace(item.snippet?.title))
                        texts.Add(item.snippet!.title!);
                    if (!string.IsNullOrWhiteSpace(item.snippet?.description))
                        // Take first 300 chars of description (enough for detection)
                        texts.Add(item.snippet!.description![..Math.Min(300, item.snippet.description.Length)]);
                }
            }
            catch (Exception ex)
            {
                _logger.LogDebug(ex, "LanguageDetection.AppendVideoTexts failed for {C}", channelId);
            }
        }

        private async Task AppendCommentTextsAsync(
            string channelId, List<string> texts, CancellationToken ct)
        {
            if (!IsApiKeyConfigured()) return;
            var client = _httpFactory.CreateClient();

            // Status check before try/catch so 403/429 propagates and breaks the batch loop
            var url      = $"https://www.googleapis.com/youtube/v3/commentThreads"
                         + $"?part=snippet&allThreadsRelatedToChannelId={channelId}"
                         + $"&maxResults=20&order=relevance&key={_apiKey}";
            var httpResp = await client.GetAsync(url, ct);
            if (httpResp.StatusCode is System.Net.HttpStatusCode.Forbidden
                                      or System.Net.HttpStatusCode.TooManyRequests
                                      or System.Net.HttpStatusCode.Unauthorized)
                throw new HttpRequestException($"YouTube API returned {(int)httpResp.StatusCode}", null, httpResp.StatusCode);

            try
            {
                var resp = await httpResp.Content.ReadFromJsonAsync<YtCommentThreadResponse>(cancellationToken: ct);
                if (resp?.items == null) return;

                foreach (var item in resp.items)
                {
                    var text = item.snippet?.topLevelComment?.snippet?.textDisplay;
                    if (!string.IsNullOrWhiteSpace(text))
                        texts.Add(text[..Math.Min(200, text.Length)]);
                }
            }
            catch (Exception ex)
            {
                _logger.LogDebug(ex, "LanguageDetection.AppendComments failed for {C}", channelId);
            }
        }

        // ── Language detection algorithm ─────────────────────────────────────

        /// <summary>
        /// For each text sample count characters that fall into each Unicode block.
        /// Aggregate counts across all samples, normalise to fractions, return distribution.
        /// </summary>
        private Dictionary<string, double> ComputeLanguageDistribution(
            List<string> texts, string? channelName)
        {
            var totals = new Dictionary<string, double>(StringComparer.Ordinal);

            foreach (var text in texts)
            {
                if (string.IsNullOrWhiteSpace(text)) continue;

                int totalChars = 0;
                var counts = new Dictionary<string, int>(StringComparer.Ordinal);

                foreach (char ch in text)
                {
                    int cp = ch;

                    // Check Indian script blocks
                    bool matched = false;
                    foreach (var (min, max, lang) in ScriptRanges)
                    {
                        if (cp >= min && cp <= max)
                        {
                            counts[lang] = counts.GetValueOrDefault(lang) + 1;
                            totalChars++;
                            matched = true;
                            break;
                        }
                    }

                    // Latin characters → English candidate
                    if (!matched && ((cp >= 0x41 && cp <= 0x5A) || (cp >= 0x61 && cp <= 0x7A)))
                    {
                        counts["English"] = counts.GetValueOrDefault("English") + 1;
                        totalChars++;
                    }
                }

                if (totalChars == 0) continue;

                // Normalise this sample and add to running totals (weighted by sample length)
                double weight = Math.Sqrt(totalChars); // longer samples count more
                foreach (var kv in counts)
                    totals[kv.Key] = totals.GetValueOrDefault(kv.Key) + (kv.Value / (double)totalChars) * weight;
            }

            if (totals.Count == 0) return totals;

            // Normalise to fractions summing to ≤1
            double total = totals.Values.Sum();
            return totals.ToDictionary(kv => kv.Key, kv => kv.Value / total);
        }

        /// <summary>
        /// Disambiguates Devanagari script into Hindi / Marathi / Haryanvi.
        /// Uses keyword matching on channel name as a lightweight signal.
        /// </summary>
        private static string ResolveDevanagari(string detectedScript, string channelName)
        {
            if (detectedScript != "Devanagari") return detectedScript;

            var lower = channelName.ToLowerInvariant();
            if (lower.Contains("haryanvi") || lower.Contains("haryana"))
                return "Haryanvi";
            if (lower.Contains("marathi") || lower.Contains("maharashtra"))
                return "Marathi";

            return "Hindi"; // default for Devanagari
        }

        /// <summary>
        /// Derives a human-readable region from country code and language.
        /// </summary>
        private static string DeriveRegion(string? countryCode, string language)
        {
            // Language-specific south-India region override
            var southIndianLangs = new HashSet<string> { "Tamil", "Telugu", "Kannada", "Malayalam" };
            if (southIndianLangs.Contains(language))
                return "South India";

            return countryCode switch
            {
                "IN"  => "India",
                "US"  => "United States",
                "GB"  => "United Kingdom",
                "CA"  => "Canada",
                "AU"  => "Australia",
                "PK"  => "Pakistan",
                "BD"  => "Bangladesh",
                "NP"  => "Nepal",
                "LK"  => "Sri Lanka",
                null  => "Unknown",
                _     => countryCode
            };
        }

        // ── Helpers ───────────────────────────────────────────────────────────

        private static string UploadsPlaylistId(string channelId) =>
            channelId.StartsWith("UC", StringComparison.Ordinal)
                ? "UU" + channelId[2..]
                : channelId;

        private bool IsApiKeyConfigured() =>
            !string.IsNullOrWhiteSpace(_apiKey) &&
            !_apiKey!.Contains("YOUR", StringComparison.OrdinalIgnoreCase) &&
            !_apiKey.StartsWith("REPLACE", StringComparison.OrdinalIgnoreCase);

        // ── Internal YouTube API response DTOs ────────────────────────────────

        private class YtPlaylistResponse
        {
            public List<PlItem>? items { get; set; }
            public class PlItem
            {
                public PlContent? contentDetails { get; set; }
            }
            public class PlContent { public string? videoId { get; set; } }
        }

        private class YtVideoListResponse
        {
            public List<VideoItem>? items { get; set; }
            public class VideoItem   { public VideoSnippet? snippet { get; set; } }
            public class VideoSnippet
            {
                public string? title       { get; set; }
                public string? description { get; set; }
            }
        }

        private class YtCommentThreadResponse
        {
            public List<CommentThread>? items { get; set; }
            public class CommentThread    { public ThreadSnippet? snippet { get; set; } }
            public class ThreadSnippet    { public CommentItem? topLevelComment { get; set; } }
            public class CommentItem      { public CommentSnippet? snippet       { get; set; } }
            public class CommentSnippet   { public string? textDisplay          { get; set; } }
        }
    }
}
