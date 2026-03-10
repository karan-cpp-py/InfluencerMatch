using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using InfluencerMatch.Application.DTOs;
using InfluencerMatch.Application.Interfaces;

namespace InfluencerMatch.Infrastructure.Services
{
    public class YouTubeVideoAnalysisService : IYouTubeVideoAnalysisService
    {
        private static readonly string[] ConfirmedCollabKeywords =
        {
            "sponsored", "paid partnership", "in collaboration with", "partnered with", "this video is sponsored"
        };

        private static readonly string[] LikelyCollabKeywords =
        {
            "affiliate", "i earn a commission", "use code", "promo code", "discount code", "sign up", "free trial", "use my link", "download"
        };

        private static readonly string[] PositiveWords = { "love", "great", "awesome", "helpful", "amazing", "good", "best", "nice", "super" };
        private static readonly string[] NegativeWords = { "bad", "worst", "boring", "fake", "hate", "waste", "misleading", "poor", "scam" };
        private static readonly string[] ProfanityWords = { "fuck", "shit", "bitch", "asshole", "bastard" };

        public Task<object> AnalyzeLatestVideoAsync(YouTubeVideoAnalysisRequestDto request)
        {
            var today = request.TodayUtc ?? DateTime.UtcNow;
            var video = request.Video ?? new YouTubeVideoDto();
            var stats = video.Statistics ?? new YouTubeVideoStatisticsDto();

            var title = video.Title ?? string.Empty;
            var description = video.Description ?? string.Empty;
            var tags = video.Tags ?? new List<string>();
            var allEvidenceText = string.Join("\n", new[] { title, description, string.Join(" ", tags) }.Where(x => !string.IsNullOrWhiteSpace(x))).ToLowerInvariant();

            var collaboration = DetectCollaboration(allEvidenceText, description, tags, request.Comments);
            var growth = BuildGrowth(stats, request.TimeSeries);
            var comments = BuildCommentIntelligence(request.Comments);
            var brandReadout = BuildBrandReadout(video, collaboration, comments);

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

            var missingDataChecklist = BuildMissingChecklist(video, request.ChannelContext, request.Comments, request.TimeSeries);

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
                comment_intelligence = comments,
                brand_agency_readout = brandReadout,
                recommendations = recommendations,
                missing_data_checklist = missingDataChecklist,
                ui_widget_ideas = uiIdeas
            };

            return Task.FromResult(output);
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

        private static object BuildCommentIntelligence(List<YouTubeCommentDto> comments)
        {
            var texts = comments.Select(c => c.TextOriginal ?? string.Empty).Where(t => !string.IsNullOrWhiteSpace(t)).ToList();
            if (texts.Count == 0)
            {
                return new
                {
                    overall_sentiment = "Unclear due to limited sample",
                    sentiment_breakdown = new { positive_pct = 0, mixed_pct = 0, negative_pct = 0 },
                    top_5_themes = new List<string>(),
                    improvement_suggestions = new[] { "Collect at least 30 meaningful comments for reliable voice-of-viewer analysis." },
                    audience_questions = new List<string>(),
                    brand_safety = new
                    {
                        profanity = "Unclear due to limited sample",
                        controversy = "Unclear due to limited sample",
                        misinformation_claims = "Unclear due to limited sample",
                        harassment = "Unclear due to limited sample"
                    }
                };
            }

            int pos = 0, neg = 0;
            foreach (var t in texts.Select(x => x.ToLowerInvariant()))
            {
                if (PositiveWords.Any(t.Contains)) pos++;
                if (NegativeWords.Any(t.Contains)) neg++;
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
                brand_safety = new
                {
                    profanity = profanityDetected ? "Detected" : "Not detected",
                    controversy = "Unclear due to limited sample",
                    misinformation_claims = "Unclear due to limited sample",
                    harassment = "Unclear due to limited sample"
                }
            };
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
