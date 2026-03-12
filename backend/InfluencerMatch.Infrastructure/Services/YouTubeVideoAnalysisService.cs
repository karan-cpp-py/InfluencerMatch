using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using InfluencerMatch.Application.DTOs;
using InfluencerMatch.Application.Interfaces;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace InfluencerMatch.Infrastructure.Services
{
    public class YouTubeVideoAnalysisService : IYouTubeVideoAnalysisService
    {
        private readonly IHttpClientFactory _http;
        private readonly ILogger<YouTubeVideoAnalysisService> _logger;
        private readonly string? _apiKey;

        private static readonly string[] ConfirmedCollabKeywords =
        {
            "sponsored", "paid partnership", "in collaboration with", "partnered with", "this video is sponsored"
        };

        private static readonly string[] LikelyCollabKeywords =
        {
            "affiliate", "i earn a commission", "use code", "promo code", "discount code", "sign up", "free trial", "use my link", "download"
        };

        private static readonly Dictionary<string, double> PositiveWords = new(StringComparer.OrdinalIgnoreCase)
        {
            ["love"] = 2.0, ["great"] = 1.8, ["awesome"] = 1.9, ["helpful"] = 1.6, ["amazing"] = 2.0,
            ["good"] = 1.2, ["best"] = 1.5, ["nice"] = 1.1, ["super"] = 1.2, ["excellent"] = 2.0,
            ["clear"] = 1.0, ["informative"] = 1.4, ["thanks"] = 1.1, ["thank you"] = 1.4,
        };
        private static readonly Dictionary<string, double> NegativeWords = new(StringComparer.OrdinalIgnoreCase)
        {
            ["bad"] = 1.5, ["worst"] = 2.0, ["boring"] = 1.4, ["fake"] = 1.8, ["hate"] = 2.0,
            ["waste"] = 1.7, ["misleading"] = 1.8, ["poor"] = 1.6, ["scam"] = 2.0,
            ["confusing"] = 1.2, ["unclear"] = 1.1, ["clickbait"] = 1.6,
        };

        private static readonly Dictionary<string, string[]> EmotionLexicon = new(StringComparer.OrdinalIgnoreCase)
        {
            ["joy"] = new[] { "love", "great", "awesome", "amazing", "happy", "enjoy", "fun", "best" },
            ["anger"] = new[] { "hate", "worst", "scam", "fake", "angry", "annoying", "trash" },
            ["sadness"] = new[] { "sad", "disappointed", "upset", "unfortunately" },
            ["fear"] = new[] { "afraid", "scared", "worry", "dangerous", "risk" },
            ["surprise"] = new[] { "wow", "unexpected", "surprised", "shocking" },
        };

        private static readonly string[] ProfanityWords = { "fuck", "shit", "bitch", "asshole", "bastard" };

        private record YtCommentThreadsResponse(List<YtCommentItem>? items, string? nextPageToken, PageInfo? pageInfo);
        private record YtCommentItem(YtCommentSnippetWrap? snippet);
        private record YtCommentSnippetWrap(YtTopLevelComment? topLevelComment);
        private record YtTopLevelComment(YtTopLevelCommentSnippet? snippet);
        private record YtTopLevelCommentSnippet(string? authorDisplayName, string? textOriginal, long? likeCount, DateTime? publishedAt);
        private record PageInfo(int? totalResults, int? resultsPerPage);

        public YouTubeVideoAnalysisService(
            IHttpClientFactory http,
            IConfiguration config,
            ILogger<YouTubeVideoAnalysisService> logger)
        {
            _http = http;
            _logger = logger;
            _apiKey =
                config["YouTube:ApiKey"]
                ?? config["YouTube__ApiKey"]
                ?? config["YOUTUBE_API_KEY"];
        }

        public async Task<object> AnalyzeLatestVideoAsync(YouTubeVideoAnalysisRequestDto request)
        {
            var today = request.TodayUtc ?? DateTime.UtcNow;
            var video = request.Video ?? new YouTubeVideoDto();
            var stats = video.Statistics ?? new YouTubeVideoStatisticsDto();

            var inputComments = request.Comments ?? new List<YouTubeCommentDto>();
            var comments = inputComments;

            var maxFetch = Math.Clamp(request.MaxCommentsToFetch <= 0 ? 500 : request.MaxCommentsToFetch, 50, 2000);
            var commentFetchMeta = new
            {
                mode = "provided",
                requested_max = maxFetch,
                provided_count = inputComments.Count,
                fetched_count = 0,
                total_comment_count = stats.CommentCount,
                fetched_all = inputComments.Count > 0 && stats.CommentCount.HasValue && inputComments.Count >= stats.CommentCount.Value,
                capped = false,
                note = inputComments.Count > 0
                    ? "Using comments provided in request payload."
                    : "No comments provided; comment-level NLP is limited until comments are fetched or passed in payload."
            };

            if ((comments == null || comments.Count == 0)
                && request.AutoFetchComments
                && !string.IsNullOrWhiteSpace(video.VideoId)
                && IsApiKeyConfigured())
            {
                var fetched = await FetchCommentsForVideoAsync(video.VideoId!, maxFetch);
                comments = fetched.Comments;
                commentFetchMeta = new
                {
                    mode = "youtube_comment_threads_api",
                    requested_max = maxFetch,
                    provided_count = inputComments.Count,
                    fetched_count = fetched.Comments.Count,
                    total_comment_count = fetched.TotalResults ?? stats.CommentCount,
                    fetched_all = fetched.FetchedAll,
                    capped = fetched.Capped,
                    note = fetched.Note
                };
            }

            var title = video.Title ?? string.Empty;
            var description = video.Description ?? string.Empty;
            var tags = video.Tags ?? new List<string>();
            var allEvidenceText = string.Join("\n", new[] { title, description, string.Join(" ", tags) }.Where(x => !string.IsNullOrWhiteSpace(x))).ToLowerInvariant();

            var normalizedComments = comments ?? new List<YouTubeCommentDto>();

            var collaboration = DetectCollaboration(allEvidenceText, description, tags, normalizedComments);
            var growth = BuildGrowth(stats, request.TimeSeries);
            var commentIntelligence = BuildCommentIntelligence(normalizedComments, stats.CommentCount, commentFetchMeta);
            var brandReadout = BuildBrandReadout(video, collaboration, commentIntelligence);

            var videoSummary = new
            {
                summary = BuildSummary(title, description),
                metadata = new
                {
                    publish_date = video.PublishedAt,
                    tags,
                    category_id = video.CategoryId,
                    language = video.DefaultLanguage ?? video.DefaultAudioLanguage,
                    duration = video.Duration
                },
                analysis_context = new
                {
                    creator_name = request.CreatorName,
                    channel_id = request.ChannelId,
                    analyzed_at_utc = today
                }
            };

            var recommendations = new
            {
                for_creator = new[]
                {
                    "Reply to top audience questions within 24 hours to improve community trust and comment depth.",
                    "Test stronger first-20-second hooks and compare retention on the next 3 uploads.",
                    "If collaboration content is used, add clear and early disclosure language in title/description.",
                    "Use comment themes to plan next upload topics and add a direct CTA for suggested topics.",
                    "Track 24h and 7d performance versus your own channel baseline before changing format."
                },
                for_brands_agencies = new[]
                {
                    "Request 3-month channel baseline metrics before deal finalization.",
                    "Use integrated mention format first, then scale to dedicated videos if audience response is positive.",
                    "Align campaign CTA with audience questions/themes already present in comments.",
                    "Verify disclosure language and destination links in description before campaign goes live.",
                    "Include post-campaign measurement plan: view quality, comment quality, and CTA conversions."
                }
            };

            var missingDataChecklist = BuildMissingChecklist(video, request.ChannelContext, normalizedComments, request.TimeSeries);

            var uiIdeas = new[]
            {
                "Collaboration badge with status + confidence",
                "Evidence panel with quoted phrases and detected link patterns",
                "Engagement ratio cards (likes/views, comments/views)",
                "24h-7d growth curve with velocity markers",
                "Comment sentiment gauge and sample size",
                "Top viewer themes list",
                "Audience questions queue for creator replies",
                "Brand-safety indicator with detected/unclear flags"
            };

            object output = new
            {
                video_summary = videoSummary,
                collaboration_detection = collaboration,
                growth_analysis = growth,
                comment_intelligence = commentIntelligence,
                brand_agency_readout = brandReadout,
                recommendations = recommendations,
                missing_data_checklist = missingDataChecklist,
                ui_widget_ideas = uiIdeas
            };

            return output;
        }

        private static string BuildSummary(string title, string description)
        {
            var descFirst = string.IsNullOrWhiteSpace(description)
                ? "Description not provided."
                : description.Split(new[] { '.', '\n' }, StringSplitOptions.RemoveEmptyEntries).FirstOrDefault()?.Trim() ?? "Description available but unclear.";

            if (string.IsNullOrWhiteSpace(title) && string.IsNullOrWhiteSpace(description))
            {
                return "Uncertain: video summary cannot be inferred because title and description are missing.";
            }

            return $"The video appears to cover: '{title}'. {descFirst}";
        }

        private static object DetectCollaboration(string allText, string description, List<string> tags, List<YouTubeCommentDto> comments)
        {
            var evidence = new List<string>();
            var brands = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            foreach (var k in ConfirmedCollabKeywords)
            {
                if (allText.Contains(k))
                {
                    evidence.Add($"keyword: '{k}'");
                }
            }

            foreach (var k in LikelyCollabKeywords)
            {
                if (allText.Contains(k))
                {
                    evidence.Add($"signal: '{k}'");
                }
            }

            var links = Regex.Matches(description ?? string.Empty, @"https?://[^\s)]+", RegexOptions.IgnoreCase)
                .Select(m => m.Value)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .Take(5)
                .ToList();

            foreach (var link in links)
            {
                evidence.Add($"link: '{Trim(link, 20)}'");
                var host = TryHost(link);
                if (!string.IsNullOrWhiteSpace(host)) brands.Add(host!);
            }

            foreach (var tag in tags)
            {
                if (tag.StartsWith("#ad", StringComparison.OrdinalIgnoreCase) || tag.StartsWith("#sponsored", StringComparison.OrdinalIgnoreCase))
                {
                    evidence.Add($"tag: '{Trim(tag, 20)}'");
                }
            }

            var topCommentText = string.Join(" ", comments.Take(10).Select(c => c.TextOriginal ?? string.Empty)).ToLowerInvariant();
            if (topCommentText.Contains("sponsored") || topCommentText.Contains("ad"))
            {
                evidence.Add("comments mention sponsorship language");
            }

            var confirmed = evidence.Any(e => e.Contains("keyword:"));
            var likely = evidence.Count >= 2;

            var status = confirmed
                ? "Confirmed"
                : likely
                    ? "Likely"
                    : evidence.Count == 1
                        ? "Unclear"
                        : "No evidence";

            var confidence = confirmed ? 85 : likely ? 65 : evidence.Count == 1 ? 40 : 15;
            var reason = confirmed
                ? "Explicit sponsorship/disclosure phrases found in metadata."
                : likely
                    ? "Multiple affiliate/CTA/link patterns found, but no explicit paid-disclosure phrase."
                    : evidence.Count == 1
                        ? "Single weak signal found; uncertain without stronger disclosure evidence."
                        : "No sponsorship keywords, affiliate patterns, or brand CTA signals detected.";

            return new
            {
                collaboration_status = status,
                brands_detected = brands.ToList(),
                evidence = evidence.Select(x => Trim(x, 20)).ToList(),
                confidence_score = confidence,
                confidence_reason = reason
            };
        }

        private static object BuildGrowth(YouTubeVideoStatisticsDto stats, List<YouTubeTimeSeriesPointDto> timeSeries)
        {
            var views = stats.ViewCount ?? 0;
            var likes = stats.LikeCount ?? 0;
            var comments = stats.CommentCount ?? 0;

            var likeView = views > 0 ? Math.Round((double)likes / views, 4) : 0;
            var commentView = views > 0 ? Math.Round((double)comments / views, 4) : 0;

            if (timeSeries == null || timeSeries.Count < 2)
            {
                return new
                {
                    performance_snapshot = new { view_count = views, like_count = likes, comment_count = comments },
                    engagement_ratios = new { likes_per_view = likeView, comments_per_view = commentView },
                    early_growth_indicators = new
                    {
                        status = "limited",
                        note = "Uncertain: only single snapshot available. Provide time-series points (1h/6h/24h/48h/7d) for growth slope analysis."
                    },
                    benchmarks = "Channel baseline averages not available; compare against creator's own last 10 videos."
                };
            }

            var ordered = timeSeries.OrderBy(t => t.TimestampUtc).ToList();
            var first = ordered.First();
            var last = ordered.Last();
            var hours = Math.Max(1, (last.TimestampUtc - first.TimestampUtc).TotalHours);
            var deltaViews = (last.ViewCount ?? 0) - (first.ViewCount ?? 0);
            var viewsPerHour = Math.Round(deltaViews / hours, 2);

            var first24Cutoff = first.TimestampUtc.AddHours(24);
            var first24 = ordered.Where(x => x.TimestampUtc <= first24Cutoff).LastOrDefault() ?? first;

            return new
            {
                performance_snapshot = new { view_count = views, like_count = likes, comment_count = comments },
                engagement_ratios = new { likes_per_view = likeView, comments_per_view = commentView },
                early_growth_indicators = new
                {
                    first_24h_views = first24.ViewCount,
                    views_per_hour = viewsPerHour,
                    trend = viewsPerHour > 0 ? "growing" : "flat_or_declining"
                },
                benchmarks = "Channel baseline averages not available; compare against creator's own last 10 videos."
            };
        }

        private static object BuildCommentIntelligence(List<YouTubeCommentDto> comments, long? totalCommentCount, object fetchMeta)
        {
            var texts = comments.Select(c => c.TextOriginal ?? string.Empty).Where(t => !string.IsNullOrWhiteSpace(t)).ToList();
            if (texts.Count == 0)
            {
                return new
                {
                    overall_sentiment = "Unclear due to limited sample",
                    sentiment_breakdown = new { positive_pct = 0, mixed_pct = 0, negative_pct = 0 },
                    emotion_breakdown = new { joy_pct = 0, anger_pct = 0, sadness_pct = 0, fear_pct = 0, surprise_pct = 0, neutral_pct = 100 },
                    top_5_themes = new List<string>(),
                    improvement_suggestions = new[] { "Collect at least 30 meaningful comments for reliable voice-of-viewer analysis." },
                    audience_questions = new List<string>(),
                    sample_coverage = new
                    {
                        sample_size = 0,
                        total_comment_count = totalCommentCount,
                        fetch = fetchMeta
                    },
                    brand_safety = new
                    {
                        profanity = "Unclear due to limited sample",
                        controversy = "Unclear due to limited sample",
                        misinformation_claims = "Unclear due to limited sample",
                        harassment = "Unclear due to limited sample"
                    }
                };
            }

            int pos = 0, neg = 0, neutral = 0;
            var emotionCounts = new Dictionary<string, int>
            {
                ["joy"] = 0,
                ["anger"] = 0,
                ["sadness"] = 0,
                ["fear"] = 0,
                ["surprise"] = 0,
                ["neutral"] = 0,
            };

            foreach (var t in texts.Select(x => x.ToLowerInvariant()))
            {
                var score = ScoreTextSentiment(t);
                if (score > 0.35) pos++;
                else if (score < -0.35) neg++;
                else neutral++;

                var dominantEmotion = DetectDominantEmotion(t);
                emotionCounts[dominantEmotion] += 1;
            }

            var total = Math.Max(1, texts.Count);
            var posPct = (int)Math.Round(pos * 100.0 / total);
            var negPct = (int)Math.Round(neg * 100.0 / total);
            var mixedPct = Math.Max(0, 100 - posPct - negPct);

            var sentiment = posPct >= 60 ? "Positive" : negPct >= 40 ? "Negative" : "Mixed";

            var themes = ExtractThemes(texts).Take(5).ToList();
            var questions = texts.Where(t => t.Contains('?')).Take(6).Select(t => Trim(t, 20)).ToList();
            var profanityDetected = texts.Any(t => ProfanityWords.Any(p => t.Contains(p, StringComparison.OrdinalIgnoreCase)));

            return new
            {
                overall_sentiment = sentiment,
                sentiment_breakdown = new { positive_pct = posPct, mixed_pct = mixedPct, negative_pct = negPct },
                emotion_breakdown = new
                {
                    joy_pct = (int)Math.Round(emotionCounts["joy"] * 100.0 / total),
                    anger_pct = (int)Math.Round(emotionCounts["anger"] * 100.0 / total),
                    sadness_pct = (int)Math.Round(emotionCounts["sadness"] * 100.0 / total),
                    fear_pct = (int)Math.Round(emotionCounts["fear"] * 100.0 / total),
                    surprise_pct = (int)Math.Round(emotionCounts["surprise"] * 100.0 / total),
                    neutral_pct = (int)Math.Round(emotionCounts["neutral"] * 100.0 / total),
                },
                top_5_themes = themes,
                improvement_suggestions = new[]
                {
                    "Answer repeated viewer questions in pinned comment or next video.",
                    "Convert top-requested theme into next upload topic.",
                    "Clarify unclear parts viewers mention in comments.",
                    "Add chapter timestamps for high-friction sections.",
                    "Encourage specific feedback CTA in end-screen/comment pin."
                },
                audience_questions = questions,
                sample_coverage = new
                {
                    sample_size = texts.Count,
                    total_comment_count = totalCommentCount,
                    fetch = fetchMeta,
                },
                brand_safety = new
                {
                    profanity = profanityDetected ? "Detected" : "Not detected",
                    controversy = "Unclear due to limited sample",
                    misinformation_claims = "Unclear due to limited sample",
                    harassment = "Unclear due to limited sample"
                }
            };
        }

        private static double ScoreTextSentiment(string text)
        {
            var score = 0.0;

            foreach (var kv in PositiveWords)
            {
                if (text.Contains(kv.Key, StringComparison.OrdinalIgnoreCase))
                    score += kv.Value;
            }

            foreach (var kv in NegativeWords)
            {
                if (text.Contains(kv.Key, StringComparison.OrdinalIgnoreCase))
                    score -= kv.Value;
            }

            if (text.Contains("!")) score += 0.1;
            if (text.Contains("??")) score -= 0.2;

            return score;
        }

        private static string DetectDominantEmotion(string text)
        {
            var counts = EmotionLexicon.ToDictionary(k => k.Key, _ => 0);
            foreach (var kv in EmotionLexicon)
            {
                foreach (var token in kv.Value)
                {
                    if (text.Contains(token, StringComparison.OrdinalIgnoreCase))
                        counts[kv.Key] += 1;
                }
            }

            var top = counts.OrderByDescending(x => x.Value).First();
            return top.Value == 0 ? "neutral" : top.Key;
        }

        private async Task<(List<YouTubeCommentDto> Comments, bool FetchedAll, bool Capped, int? TotalResults, string Note)> FetchCommentsForVideoAsync(string videoId, int maxComments)
        {
            try
            {
                var http = _http.CreateClient();
                var comments = new List<YouTubeCommentDto>();
                string? pageToken = null;
                int? totalResults = null;

                do
                {
                    var tokenPart = string.IsNullOrWhiteSpace(pageToken) ? string.Empty : $"&pageToken={Uri.EscapeDataString(pageToken)}";
                    var url = "https://www.googleapis.com/youtube/v3/commentThreads"
                        + "?part=snippet"
                        + "&textFormat=plainText"
                        + "&maxResults=100"
                        + "&order=time"
                        + $"&videoId={Uri.EscapeDataString(videoId)}"
                        + tokenPart
                        + $"&key={Uri.EscapeDataString(_apiKey!)}";

                    var resp = await http.GetFromJsonAsync<YtCommentThreadsResponse>(url);
                    if (resp == null) break;

                    if (resp.pageInfo?.totalResults != null)
                        totalResults = resp.pageInfo.totalResults;

                    foreach (var item in resp.items ?? new List<YtCommentItem>())
                    {
                        var s = item?.snippet?.topLevelComment?.snippet;
                        if (s == null || string.IsNullOrWhiteSpace(s.textOriginal)) continue;

                        comments.Add(new YouTubeCommentDto
                        {
                            AuthorDisplayName = s.authorDisplayName,
                            LikeCount = s.likeCount,
                            PublishedAt = s.publishedAt,
                            TextOriginal = s.textOriginal,
                        });

                        if (comments.Count >= maxComments) break;
                    }

                    if (comments.Count >= maxComments) break;
                    pageToken = resp.nextPageToken;
                }
                while (!string.IsNullOrWhiteSpace(pageToken));

                var capped = comments.Count >= maxComments;
                var fetchedAll = !capped && string.IsNullOrWhiteSpace(pageToken);
                var note = capped
                    ? $"Fetched {comments.Count} comments and stopped at cap {maxComments}."
                    : $"Fetched {comments.Count} comments from YouTube CommentThreads API.";

                return (comments, fetchedAll, capped, totalResults, note);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to fetch comments for video {VideoId}", videoId);
                return (new List<YouTubeCommentDto>(), false, false, null, "Comment fetch failed; analysis used provided comments only.");
            }
        }

        private bool IsApiKeyConfigured()
        {
            if (string.IsNullOrWhiteSpace(_apiKey)) return false;
            if (_apiKey.Contains("YOUR_YOUTUBE", StringComparison.OrdinalIgnoreCase)) return false;
            if (_apiKey.Contains("YOUR", StringComparison.OrdinalIgnoreCase)) return false;
            if (_apiKey.StartsWith("REPLACE", StringComparison.OrdinalIgnoreCase)) return false;
            return true;
        }

        private static object BuildBrandReadout(YouTubeVideoDto video, object collaboration, object comments)
        {
            var categoryHint = string.IsNullOrWhiteSpace(video.CategoryId) ? "unknown" : video.CategoryId;
            var tags = video.Tags ?? new List<string>();

            return new
            {
                audience_cares_about = "Based on comment themes and recurring questions in the provided sample.",
                implied_brand_fit = new
                {
                    category_hint = categoryHint,
                    relevant_verticals = tags.Take(6).ToList()
                },
                sponsor_reaction = "Uncertain unless comments explicitly reference sponsor/disclosure language.",
                suggested_partnership_format = "Start with integrated mention + clear disclosure + single focused CTA.",
                risk_notes = new[]
                {
                    "Verify paid partnership disclosures in title/description/video overlay.",
                    "Verify destination links and tracking parameters before launch.",
                    "Confirm brand-safety checks with larger comment sample."
                },
                what_to_verify = new[]
                {
                    "Paid partnership label usage",
                    "Affiliate/referral link structure",
                    "Audience-location and language fit",
                    "Recent 10-video baseline performance"
                }
            };
        }

        private static List<string> ExtractThemes(List<string> texts)
        {
            var themeRules = new Dictionary<string, string[]>(StringComparer.OrdinalIgnoreCase)
            {
                ["Content quality praise"] = new[] { "great", "awesome", "best", "amazing", "helpful" },
                ["Requests for more videos"] = new[] { "next", "more", "part 2", "please make", "upload" },
                ["Question and clarification"] = new[] { "?", "how", "why", "what", "can you" },
                ["Pacing or clarity issues"] = new[] { "too fast", "confusing", "unclear", "explain", "slow" },
                ["Sponsor/brand mentions"] = new[] { "sponsor", "ad", "collab", "partnership", "code" }
            };

            var scores = themeRules.ToDictionary(kv => kv.Key, _ => 0);
            foreach (var t in texts.Select(x => x.ToLowerInvariant()))
            {
                foreach (var kv in themeRules)
                {
                    if (kv.Value.Any(t.Contains))
                    {
                        scores[kv.Key] += 1;
                    }
                }
            }

            return scores
                .OrderByDescending(kv => kv.Value)
                .Where(kv => kv.Value > 0)
                .Select(kv => kv.Key)
                .ToList();
        }

        private static List<string> BuildMissingChecklist(YouTubeVideoDto video, YouTubeChannelContextDto? channel, List<YouTubeCommentDto> comments, List<YouTubeTimeSeriesPointDto> series)
        {
            var missing = new List<string>();
            if (string.IsNullOrWhiteSpace(video.Title)) missing.Add("Video title missing");
            if (string.IsNullOrWhiteSpace(video.Description)) missing.Add("Video description missing");
            if (video.Tags == null || video.Tags.Count == 0) missing.Add("Video tags missing");
            if (!video.PublishedAt.HasValue) missing.Add("Video publish timestamp missing");
            if (channel?.SubscriberCount == null) missing.Add("Channel subscriberCount missing");
            if (comments == null || comments.Count < 20) missing.Add("Low comment sample (recommend >=20 for stable sentiment)");
            if (series == null || series.Count < 2) missing.Add("No time-series points for growth velocity analysis");
            return missing;
        }

        private static string? TryHost(string url)
        {
            if (Uri.TryCreate(url, UriKind.Absolute, out var u)) return u.Host;
            return null;
        }

        private static string Trim(string value, int maxWords)
        {
            var words = value.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            if (words.Length <= maxWords) return value;
            return string.Join(' ', words.Take(maxWords));
        }
    }
}
