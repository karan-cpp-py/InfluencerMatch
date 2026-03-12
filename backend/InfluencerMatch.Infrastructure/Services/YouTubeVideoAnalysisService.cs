using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Json;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
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
        private readonly string _hfModel;
        private readonly string? _hfApiToken;

        private static readonly string[] ConfirmedCollabKeywords =
        {
            "sponsored", "paid partnership", "in collaboration with", "partnered with", "this video is sponsored"
        };

        private static readonly string[] LikelyCollabKeywords =
        {
            "affiliate", "i earn a commission", "use code", "promo code", "discount code", "sign up", "free trial", "use my link", "download"
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
        private record YtSearchResponse(List<YtSearchItem>? items);
        private record YtSearchItem(YtSearchId? id);
        private record YtSearchId(string? videoId);
        private record YtVideosResponse(List<YtVideoItem>? items);
        private record YtVideoItem(string? id, YtVideoSnippet? snippet, YtVideoContentDetails? contentDetails, YtVideoStatistics? statistics, YtVideoStatus? status);
        private record YtVideoSnippet(string? title, string? description, List<string>? tags, string? categoryId, DateTime? publishedAt, string? defaultLanguage, string? defaultAudioLanguage);
        private record YtVideoContentDetails(string? duration, bool? madeForKids);
        private record YtVideoStatistics(string? viewCount, string? likeCount, string? commentCount, string? favoriteCount);
        private record YtVideoStatus(bool? madeForKids);
        private record SentimentModelAggregate(bool Succeeded, string Source, string Status, int EvaluatedCount, int PositiveCount, int NeutralCount, int NegativeCount, string? Note = null);

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

            _hfModel =
                config["HuggingFace:Model"]
                ?? config["HuggingFace__Model"]
                ?? "cardiffnlp/twitter-roberta-base-sentiment-latest";
            _hfApiToken =
                config["HuggingFace:ApiToken"]
                ?? config["HuggingFace__ApiToken"]
                ?? config["HUGGINGFACE_API_TOKEN"];
        }

        public async Task<object> AnalyzeLatestVideoAsync(YouTubeVideoAnalysisRequestDto request)
        {
            var today = request.TodayUtc ?? DateTime.UtcNow;
            var video = request.Video ?? new YouTubeVideoDto();
            var stats = video.Statistics ?? new YouTubeVideoStatisticsDto();

            var inputComments = request.Comments ?? new List<YouTubeCommentDto>();
            var comments = inputComments;
            var resolvedVideoId = video.VideoId;

            var maxFetch = Math.Clamp(request.MaxCommentsToFetch <= 0 ? 500 : request.MaxCommentsToFetch, 50, 2000);
            object commentFetchMeta = new
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

            if (string.IsNullOrWhiteSpace(resolvedVideoId)
                && request.AutoFetchComments
                && IsApiKeyConfigured()
                && !string.IsNullOrWhiteSpace(request.ChannelId))
            {
                var latestVideoIds = await ResolveLatestVideoIdsByChannelIdAsync(request.ChannelId!, 10);
                resolvedVideoId = latestVideoIds.FirstOrDefault();
                if (!string.IsNullOrWhiteSpace(resolvedVideoId))
                {
                    video.VideoId = resolvedVideoId;
                }
            }

            if (!string.IsNullOrWhiteSpace(resolvedVideoId)
                && request.AutoFetchComments
                && IsApiKeyConfigured())
            {
                await EnrichVideoFromYouTubeAsync(video, resolvedVideoId!);
                stats = video.Statistics ?? new YouTubeVideoStatisticsDto();
            }

            if ((comments == null || comments.Count == 0)
                && request.AutoFetchComments
                && !string.IsNullOrWhiteSpace(request.ChannelId)
                && IsApiKeyConfigured())
            {
                var fetched = await FetchCommentsForLatestVideosAsync(request.ChannelId!, maxFetch, 10, resolvedVideoId);
                comments = fetched.Comments;
                commentFetchMeta = new
                {
                    mode = "youtube_comment_threads_api_last_10_videos",
                    requested_max = maxFetch,
                    provided_count = inputComments.Count,
                    fetched_count = fetched.Comments.Count,
                    total_comment_count = fetched.TotalResults,
                    fetched_all = fetched.FetchedAll,
                    capped = fetched.Capped,
                    videos_considered = fetched.VideoIdsConsidered,
                    videos_with_comments = fetched.VideosWithComments,
                    note = fetched.Note
                };
            }

            var title = video.Title ?? string.Empty;
            var description = video.Description ?? string.Empty;
            var tags = video.Tags ?? new List<string>();
            var allEvidenceText = string.Join("\n", new[] { title, description, string.Join(" ", tags) }.Where(x => !string.IsNullOrWhiteSpace(x))).ToLowerInvariant();

            var normalizedComments = comments ?? new List<YouTubeCommentDto>();
            var commentTexts = normalizedComments
                .Select(c => c.TextOriginal ?? string.Empty)
                .Where(t => !string.IsNullOrWhiteSpace(t))
                .ToList();
            var sentimentModel = await AnalyzeSentimentWithModelAsync(commentTexts, Math.Min(maxFetch, 200));

            // ── NLP primitives (used by both comment intelligence & recommendations) ──
            var nlpSentiment = !sentimentModel.Succeeded
                ? (sentimentModel.Status == "insufficient_sample" ? "insufficient" : "model_unavailable")
                : sentimentModel.PositiveCount * 100.0 / Math.Max(1, sentimentModel.EvaluatedCount) >= 60 ? "positive"
                    : sentimentModel.NegativeCount * 100.0 / Math.Max(1, sentimentModel.EvaluatedCount) >= 40 ? "negative"
                    : "mixed";
            var nlpThemes = commentTexts.Count > 0 ? ExtractThemes(commentTexts).Take(5).ToList() : new List<string>();
            var nlpHasQuestions = commentTexts.Any(t => t.Contains('?'));
            var nlpProfanity = commentTexts.Any(t => ProfanityWords.Any(p => t.Contains(p, StringComparison.OrdinalIgnoreCase)));

            // ── Collab status (for recommendations) ──
            bool recConfirmedCollab = ConfirmedCollabKeywords.Any(k => allEvidenceText.Contains(k, StringComparison.OrdinalIgnoreCase));
            int recLikelySignals = LikelyCollabKeywords.Count(k => allEvidenceText.Contains(k, StringComparison.OrdinalIgnoreCase))
                + (Regex.IsMatch(description, @"https?://", RegexOptions.IgnoreCase) ? 1 : 0)
                + (tags.Any(t => t.StartsWith("#ad", StringComparison.OrdinalIgnoreCase) || t.StartsWith("#sponsored", StringComparison.OrdinalIgnoreCase)) ? 1 : 0);
            var recCollabStatus = recConfirmedCollab ? "Confirmed"
                : recLikelySignals >= 2 ? "Likely"
                : recLikelySignals == 1 ? "Unclear"
                : "No evidence";

            // ── Engagement ratios (for recommendations) ──
            var recViews = stats.ViewCount;
            var recLikes = stats.LikeCount;
            var recComments = stats.CommentCount;
            double? recLikeView = recViews.HasValue && recViews > 0 && recLikes.HasValue
                ? Math.Round((double)recLikes.Value / recViews.Value, 4) : null;
            double? recCommentView = recViews.HasValue && recViews > 0 && recComments.HasValue
                ? Math.Round((double)recComments.Value / recViews.Value, 4) : null;

            bool enrichedFromYouTube = !string.IsNullOrWhiteSpace(resolvedVideoId) && IsApiKeyConfigured();

            var collaboration = DetectCollaboration(allEvidenceText, description, tags, normalizedComments);
            var growth = BuildGrowth(stats, request.TimeSeries);
            var commentIntelligence = BuildCommentIntelligence(normalizedComments, stats.CommentCount, commentFetchMeta, sentimentModel);
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

            var missingDataChecklist = BuildMissingChecklist(video, request.ChannelContext, normalizedComments, request.TimeSeries);

            var recommendations = BuildRecommendations(
                recCollabStatus,
                recViews, recLikes, recComments,
                recLikeView, recCommentView,
                nlpSentiment, nlpThemes, nlpHasQuestions, nlpProfanity,
                commentTexts.Count, missingDataChecklist, video, request.ChannelContext);

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
                ui_widget_ideas = uiIdeas,
                enriched_from_youtube = enrichedFromYouTube
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
            long? views = stats.ViewCount;
            long? likes = stats.LikeCount;
            long? comments = stats.CommentCount;

            double? likeView = views.HasValue && views > 0 && likes.HasValue
                ? Math.Round((double)likes.Value / views.Value, 4) : null;
            double? commentView = views.HasValue && views > 0 && comments.HasValue
                ? Math.Round((double)comments.Value / views.Value, 4) : null;

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

        private static object BuildCommentIntelligence(List<YouTubeCommentDto> comments, long? totalCommentCount, object fetchMeta, SentimentModelAggregate sentimentModel)
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
                        fetch = fetchMeta,
                        sentiment_model = new
                        {
                            source = sentimentModel.Source,
                            status = "insufficient_sample",
                            model_ran = false,
                            evaluated_comments = 0,
                            note = "No comments available for model sentiment inference."
                        }
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
                var dominantEmotion = DetectDominantEmotion(t);
                emotionCounts[dominantEmotion] += 1;
            }

            var total = Math.Max(1, texts.Count);
            if (sentimentModel.Succeeded)
            {
                pos = sentimentModel.PositiveCount;
                neg = sentimentModel.NegativeCount;
                neutral = sentimentModel.NeutralCount;
                total = Math.Max(1, sentimentModel.EvaluatedCount);
            }
            else
            {
                neutral = total;
            }

            var posPct = (int)Math.Round(pos * 100.0 / total);
            var negPct = (int)Math.Round(neg * 100.0 / total);
            var mixedPct = Math.Max(0, 100 - posPct - negPct);

            var sentiment = sentimentModel.Succeeded
                ? (posPct >= 60 ? "Positive" : negPct >= 40 ? "Negative" : "Mixed")
                : "Model unavailable";

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
                    sentiment_model = new
                    {
                        source = sentimentModel.Source,
                        status = sentimentModel.Status,
                        model_ran = sentimentModel.Succeeded,
                        evaluated_comments = sentimentModel.EvaluatedCount,
                        note = sentimentModel.Note
                    }
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

        private async Task<SentimentModelAggregate> AnalyzeSentimentWithModelAsync(List<string> texts, int maxSamples)
        {
            var source = $"huggingface:{_hfModel}";
            if (texts == null || texts.Count == 0)
            {
                return new SentimentModelAggregate(false, source, "insufficient_sample", 0, 0, 0, 0, "No comments to evaluate.");
            }

            var sample = texts
                .Where(t => !string.IsNullOrWhiteSpace(t))
                .Select(NormalizeCommentForModel)
                .Where(t => !string.IsNullOrWhiteSpace(t))
                .Take(Math.Max(1, maxSamples))
                .ToList();
            if (sample.Count == 0)
            {
                return new SentimentModelAggregate(false, source, "insufficient_sample", 0, 0, 0, 0, "No non-empty comments to evaluate.");
            }

            try
            {
                var http = _http.CreateClient();
                if (!string.IsNullOrWhiteSpace(_hfApiToken))
                {
                    http.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", _hfApiToken);
                }

                int pos = 0, neg = 0, neutral = 0, failed = 0;

                foreach (var text in sample)
                {
                    var payload = JsonSerializer.Serialize(new
                    {
                        inputs = text,
                        options = new
                        {
                            wait_for_model = true,
                            use_cache = true
                        }
                    });

                    using var req = new HttpRequestMessage(HttpMethod.Post, $"https://router.huggingface.co/hf-inference/models/{Uri.EscapeDataString(_hfModel)}")
                    {
                        Content = new StringContent(payload, Encoding.UTF8, "application/json")
                    };

                    using var res = await http.SendAsync(req);
                    if (!res.IsSuccessStatusCode)
                    {
                        var body = await res.Content.ReadAsStringAsync();
                        _logger.LogWarning("HuggingFace sentiment call failed: {StatusCode} {Body}", (int)res.StatusCode, body);
                        failed++;
                        continue;
                    }

                    var json = await res.Content.ReadAsStringAsync();
                    var label = ParseSentimentLabel(json);
                    switch (label)
                    {
                        case "positive": pos++; break;
                        case "negative": neg++; break;
                        default: neutral++; break;
                    }
                }

                var succeededCount = pos + neg + neutral;
                if (succeededCount == 0)
                {
                    return new SentimentModelAggregate(false, source, "api_error", 0, 0, 0, 0, $"Model inference failed for all {sample.Count} comments.");
                }

                var note = failed > 0
                    ? $"Model evaluated {succeededCount} comments successfully; skipped {failed} comments rejected by provider."
                    : null;

                return new SentimentModelAggregate(true, source, failed > 0 ? "partial_success" : "ok", succeededCount, pos, neutral, neg, note);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "HuggingFace sentiment inference failed for model {Model}", _hfModel);
                return new SentimentModelAggregate(false, source, "exception", 0, 0, 0, 0, "Model inference request failed.");
            }
        }

        private static string NormalizeCommentForModel(string text)
        {
            if (string.IsNullOrWhiteSpace(text)) return string.Empty;

            var normalized = Regex.Replace(text, @"\s+", " ").Trim();
            const int maxChars = 450;
            return normalized.Length <= maxChars
                ? normalized
                : normalized[..maxChars];
        }

        private static string ParseSentimentLabel(string json)
        {
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;

            JsonElement entries;
            if (root.ValueKind == JsonValueKind.Object && root.TryGetProperty("value", out var valueProp) && valueProp.ValueKind == JsonValueKind.Array)
            {
                entries = valueProp;
            }
            else if (root.ValueKind == JsonValueKind.Array && root.GetArrayLength() > 0)
            {
                var first = root[0];
                if (first.ValueKind == JsonValueKind.Array)
                {
                    entries = first;
                }
                else
                {
                    entries = root;
                }
            }
            else
            {
                return "neutral";
            }

            string bestLabel = "neutral";
            double bestScore = double.MinValue;

            foreach (var item in entries.EnumerateArray())
            {
                if (item.ValueKind != JsonValueKind.Object) continue;
                if (!item.TryGetProperty("label", out var labelProp)) continue;

                var labelRaw = labelProp.GetString() ?? string.Empty;
                var label = labelRaw.ToLowerInvariant();
                double score = item.TryGetProperty("score", out var scoreProp) && scoreProp.TryGetDouble(out var s) ? s : 0;

                if (score > bestScore)
                {
                    bestScore = score;
                    bestLabel = label;
                }
            }

            if (bestLabel.Contains("positive") || bestLabel == "label_2") return "positive";
            if (bestLabel.Contains("negative") || bestLabel == "label_0") return "negative";
            return "neutral";
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

        private async Task<(List<YouTubeCommentDto> Comments, bool FetchedAll, bool Capped, int? TotalResults, int VideoIdsConsidered, int VideosWithComments, string Note)> FetchCommentsForLatestVideosAsync(
            string channelId,
            int maxComments,
            int maxVideos,
            string? preferredVideoId)
        {
            var ids = await ResolveLatestVideoIdsByChannelIdAsync(channelId, Math.Max(1, maxVideos));
            if (!string.IsNullOrWhiteSpace(preferredVideoId) && !ids.Contains(preferredVideoId!, StringComparer.OrdinalIgnoreCase))
            {
                ids.Insert(0, preferredVideoId!);
            }

            ids = ids
                .Where(x => !string.IsNullOrWhiteSpace(x))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .Take(Math.Max(1, maxVideos))
                .ToList();

            if (ids.Count == 0)
            {
                return (new List<YouTubeCommentDto>(), false, false, null, 0, 0, "Could not resolve latest video IDs for channel.");
            }

            var merged = new List<YouTubeCommentDto>();
            int? totalResults = 0;
            var fetchedAll = true;
            var capped = false;
            var videosWithComments = 0;

            for (var i = 0; i < ids.Count; i++)
            {
                var remaining = maxComments - merged.Count;
                if (remaining <= 0)
                {
                    capped = true;
                    break;
                }

                var videosLeft = ids.Count - i;
                var perVideoCap = Math.Max(20, (int)Math.Ceiling((double)remaining / Math.Max(1, videosLeft)));
                var fetched = await FetchCommentsForVideoAsync(ids[i], perVideoCap);

                if (fetched.Comments.Count > 0) videosWithComments++;
                merged.AddRange(fetched.Comments);

                if (!fetched.FetchedAll) fetchedAll = false;
                if (fetched.Capped) capped = true;

                if (fetched.TotalResults.HasValue)
                {
                    totalResults = (totalResults ?? 0) + fetched.TotalResults.Value;
                }
                else
                {
                    totalResults = null;
                }
            }

            if (merged.Count > maxComments)
            {
                merged = merged.Take(maxComments).ToList();
                capped = true;
            }

            var note = $"Fetched {merged.Count} comments across latest {ids.Count} videos for creator-level analysis.";
            return (merged, fetchedAll && !capped, capped, totalResults, ids.Count, videosWithComments, note);
        }

        private async Task<string?> ResolveLatestVideoIdByChannelIdAsync(string channelId)
        {
            var ids = await ResolveLatestVideoIdsByChannelIdAsync(channelId, 1);
            return ids.FirstOrDefault();
        }

        private async Task<List<string>> ResolveLatestVideoIdsByChannelIdAsync(string channelId, int maxResults)
        {
            try
            {
                var http = _http.CreateClient();
                var url = "https://www.googleapis.com/youtube/v3/search"
                    + "?part=snippet"
                    + "&type=video"
                    + "&order=date"
                    + $"&maxResults={Math.Clamp(maxResults, 1, 50)}"
                    + $"&channelId={Uri.EscapeDataString(channelId)}"
                    + $"&key={Uri.EscapeDataString(_apiKey!)}";

                var resp = await http.GetFromJsonAsync<YtSearchResponse>(url);
                return (resp?.items ?? new List<YtSearchItem>())
                    .Select(x => x?.id?.videoId)
                    .Where(x => !string.IsNullOrWhiteSpace(x))
                    .Select(x => x!)
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .ToList();
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to resolve latest videoIds for channel {ChannelId}", channelId);
                return new List<string>();
            }
        }

        private async Task EnrichVideoFromYouTubeAsync(YouTubeVideoDto video, string videoId)
        {
            try
            {
                var http = _http.CreateClient();
                var url = "https://www.googleapis.com/youtube/v3/videos"
                    + "?part=snippet,contentDetails,statistics,status"
                    + $"&id={Uri.EscapeDataString(videoId)}"
                    + $"&key={Uri.EscapeDataString(_apiKey!)}";

                var resp = await http.GetFromJsonAsync<YtVideosResponse>(url);
                var item = resp?.items?.FirstOrDefault();
                if (item == null) return;

                var snippet = item.snippet;
                var details = item.contentDetails;
                var ytStats = item.statistics;

                if (string.IsNullOrWhiteSpace(video.VideoId)) video.VideoId = item.id;
                if (string.IsNullOrWhiteSpace(video.Title) || video.Title.StartsWith("Latest upload by", StringComparison.OrdinalIgnoreCase))
                    video.Title = snippet?.title ?? video.Title;
                if (string.IsNullOrWhiteSpace(video.Description) || video.Description.StartsWith("Auto-generated", StringComparison.OrdinalIgnoreCase))
                    video.Description = snippet?.description ?? video.Description;
                if ((video.Tags == null || video.Tags.Count == 0) && snippet?.tags != null)
                    video.Tags = snippet.tags;
                if (string.IsNullOrWhiteSpace(video.CategoryId)) video.CategoryId = snippet?.categoryId;
                if (!video.PublishedAt.HasValue) video.PublishedAt = snippet?.publishedAt;
                if (string.IsNullOrWhiteSpace(video.Duration)) video.Duration = details?.duration;
                if (!video.MadeForKids.HasValue && item.status?.madeForKids.HasValue == true) video.MadeForKids = item.status.madeForKids;
                if (string.IsNullOrWhiteSpace(video.DefaultLanguage)) video.DefaultLanguage = snippet?.defaultLanguage;
                if (string.IsNullOrWhiteSpace(video.DefaultAudioLanguage)) video.DefaultAudioLanguage = snippet?.defaultAudioLanguage;

                video.Statistics ??= new YouTubeVideoStatisticsDto();
                if (!video.Statistics.ViewCount.HasValue) video.Statistics.ViewCount = ParseNullableLong(ytStats?.viewCount);
                if (!video.Statistics.LikeCount.HasValue) video.Statistics.LikeCount = ParseNullableLong(ytStats?.likeCount);
                if (!video.Statistics.CommentCount.HasValue) video.Statistics.CommentCount = ParseNullableLong(ytStats?.commentCount);
                if (!video.Statistics.FavoriteCount.HasValue) video.Statistics.FavoriteCount = ParseNullableLong(ytStats?.favoriteCount);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to enrich video metadata for videoId {VideoId}", videoId);
            }
        }

        private static long? ParseNullableLong(string? value)
        {
            if (string.IsNullOrWhiteSpace(value)) return null;
            return long.TryParse(value, out var parsed) ? parsed : null;
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

        private static object BuildRecommendations(
            string collabStatus,
            long? views, long? likes, long? commentCount,
            double? likeView, double? commentView,
            string sentiment,
            List<string> themes,
            bool hasQuestions,
            bool profanityDetected,
            int sampleSize,
            List<string> missingData,
            YouTubeVideoDto video,
            YouTubeChannelContextDto? channel)
        {
            var forCreator = new List<string>();
            var forBrands = new List<string>();

            // ── Engagement ratios ──
            if (views.HasValue && views > 0)
            {
                if (likeView.HasValue && likeView >= 0.04)
                    forCreator.Add($"Strong like rate ({likeView.Value:P1} likes/view) — maintain this content format and use it as your engagement benchmark.");
                else if (likeView.HasValue && likeView > 0 && likeView < 0.02)
                    forCreator.Add($"Like rate is {likeView.Value:P2} — add a clear like prompt in the first 30 seconds or mid-roll to lift engagement.");
                else if (!likeView.HasValue || likeView == 0)
                    forCreator.Add("Like count is unavailable — confirm likes are enabled in YouTube Studio channel settings.");

                if (commentView.HasValue && commentView >= 0.005)
                    forCreator.Add($"Good comment participation ({commentView.Value:P2} comments/view) — reply to top threads within 24 h to sustain momentum.");
                else if (commentView.HasValue && commentView > 0 && commentView < 0.005)
                    forCreator.Add("Comment rate is below 0.5% — end your video with a direct discussion question to drive reply volume.");
                else if (commentCount.HasValue && commentCount > 0 && sampleSize > 0)
                    forCreator.Add("Comments are active — pin a comment CTA to direct viewers toward your next video or community post.");
            }
            else
            {
                forCreator.Add("View count data is not available for this video — ensure the channel is public and the YouTube API key is configured.");
            }

            // ── Scale context for brands ──
            if (views.HasValue)
            {
                long v = views.Value;
                if (v < 1_000)
                    forBrands.Add("View count under 1 K — suitable for hyper-niche micro-influencer campaigns only; validate audience-fit before committing budget.");
                else if (v < 10_000)
                    forBrands.Add($"Reach of {v:N0} views (micro tier) — integrated mention format works best; track comment engagement closely.");
                else if (v < 100_000)
                    forBrands.Add($"Moderate reach at {v:N0} views — start with 1-2 video integrations and measure CTA click-rate before scaling.");
                else
                    forBrands.Add($"Strong reach at {v:N0} views — a dedicated sponsored video is cost-effective at this scale; negotiate performance bonus.");
            }
            else
            {
                forBrands.Add("View data is unavailable — request last 10-video avg. views from the creator before finalising deal terms.");
            }

            // ── Sentiment-based recommendations ──
            switch (sentiment)
            {
                case "positive":
                    forCreator.Add("Audience sentiment is strongly positive — quote top supportive comments in a community post as social proof.");
                    forBrands.Add("Positive comment sentiment — favorable environment for brand mentions; audience is receptive to creator recommendations.");
                    break;
                case "negative":
                    forCreator.Add("Negative sentiment detected — pinpoint the top 3 complaint themes in comments and address them explicitly in your next video.");
                    forBrands.Add("Negative audience sentiment found — hold campaign launch; ask creator to resolve viewer concerns first.");
                    break;
                case "mixed":
                    forCreator.Add("Mixed sentiment — check the negative comment threads for specific claims or confusion and clarify in the description.");
                    forBrands.Add("Mixed audience sentiment — run a limited 1-video trial and monitor comment quality before scaling the campaign.");
                    break;
                case "model_unavailable":
                    forCreator.Add("Sentiment model could not score part of this comment set — review the sentiment note and rerun if provider throttling persists.");
                    forBrands.Add("Sentiment model was temporarily unavailable or partially rejected comments — avoid using sentiment alone for approval until rerun succeeds.");
                    break;
                default:
                    forCreator.Add("Comment sample too small for reliable sentiment — enable comment auto-fetch (500+) to unlock accurate NLP scoring.");
                    forBrands.Add("Insufficient comment data to assess audience sentiment — request a creator-side analytics report.");
                    break;
            }

            // ── Theme-driven topics ──
            if (themes.Count > 0)
            {
                forCreator.Add($"Top comment theme is \"{ themes[0]}\" — plan your next upload around this topic and add a pinned comment asking for viewer input.");
                if (themes.Count > 1)
                    forCreator.Add($"Secondary theme \"{ themes[1]}\" is emerging — address it in a community post or video chapter to show you listen.");
                forBrands.Add($"Dominant audience interest: \"{ themes[0]}\" — align campaign messaging and CTA with this theme for higher relevance.");
            }

            // ── Audience questions ──
            if (hasQuestions)
            {
                forCreator.Add("Audience questions detected in comments — reply to the top 3 within 24 h and consider making an FAQ / follow-up clip.");
                forBrands.Add("Viewer questions present — a branded FAQ or tutorial format would resonate with this audience.");
            }

            // ── Collaboration / disclosure ──
            switch (collabStatus)
            {
                case "Confirmed":
                    forCreator.Add("Explicit sponsorship disclosure present — also toggle the paid-partnership label in YouTube Studio for compliance.");
                    forBrands.Add("Confirmed disclosure found — verify it appears above the fold in the description and is spoken within the first 60 s of the video.");
                    break;
                case "Likely":
                    forCreator.Add("Affiliate or CTA signals detected without explicit paid disclosure — add '#ad' or 'in partnership with…' text above the fold in the description.");
                    forBrands.Add("Likely affiliate signals but no explicit paid-partnership disclosure — require creator to add FTC-compliant disclosure before campaign launch.");
                    break;
                case "No evidence":
                    forBrands.Add("No existing sponsorship signals — good candidate for a first integration; still require written disclosure agreement.");
                    break;
            }

            // ── Brand safety ──
            if (profanityDetected)
            {
                forCreator.Add("Profanity detected in comments — enable YouTube Studio comment filters to protect audience experience and brand-safety rating.");
                forBrands.Add("Profanity found in comments — verify brand-safety tolerance; consider requesting a moderation commitment from the creator.");
            }

            // ── Missing data ──
            if (missingData.Any(m => m.Contains("tags")))
                forCreator.Add("Video tags are missing — add 5-10 searchable tags in YouTube Studio to improve suggested-video placement.");
            if (missingData.Any(m => m.Contains("time-series")))
                forCreator.Add("No time-series data — feed 1 h / 6 h / 24 h / 7 d snapshots to unlock growth-velocity scoring.");
            if (missingData.Any(m => m.Contains("subscriberCount")))
                forBrands.Add("Subscriber count missing — request this directly from the creator or look it up before finalising deal terms.");
            if (missingData.Any(m => m.Contains("Low comment sample")))
                forBrands.Add($"Only {sampleSize} comments analysed — increase sample to 100+ for statistically reliable brand-safety assessment.");

            if (forCreator.Count == 0)
                forCreator.Add("Provide video statistics and at least 30 comments to unlock tailored creator recommendations.");
            if (forBrands.Count == 0)
                forBrands.Add("Provide channel baseline metrics and a comment sample to unlock detailed brand/agency recommendations.");

            return new { for_creator = forCreator, for_brands_agencies = forBrands };
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
