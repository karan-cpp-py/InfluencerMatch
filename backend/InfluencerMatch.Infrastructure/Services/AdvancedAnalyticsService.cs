using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using InfluencerMatch.Application.DTOs;
using InfluencerMatch.Application.Interfaces;
using InfluencerMatch.Domain.Entities;
using InfluencerMatch.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace InfluencerMatch.Infrastructure.Services
{
    public class AdvancedAnalyticsService : IAdvancedAnalyticsService
    {
        private readonly ApplicationDbContext _db;

        public AdvancedAnalyticsService(ApplicationDbContext db)
        {
            _db = db;
        }

        public async Task<CreatorInsightsDto?> GetCreatorInsightsAsync(
            int creatorId,
            string? brandCategory = null,
            string? brandCountry = null,
            string? brandLanguage = null,
            CancellationToken ct = default)
        {
            var creator = await _db.Creators.AsNoTracking()
                .FirstOrDefaultAsync(c => c.CreatorId == creatorId, ct);
            if (creator == null) return null;

            var videos = await _db.Videos.AsNoTracking()
                .Where(v => v.CreatorId == creatorId)
                .OrderByDescending(v => v.PublishedAt)
                .Take(40)
                .ToListAsync(ct);

            var growth = await _db.CreatorGrowth.AsNoTracking()
                .Where(g => g.CreatorId == creatorId)
                .OrderByDescending(g => g.RecordedAt)
                .Take(60)
                .ToListAsync(ct);

            var health = BuildHealth(creator, videos, growth, brandCategory);
            var audience = BuildAudience(creator, videos);
            var coaching = BuildCoaching(videos);

            await UpsertCreatorHealthSnapshotAsync(creatorId, health, audience, coaching, ct);

            return new CreatorInsightsDto
            {
                HealthScorecard = health,
                AudienceQuality = audience,
                Coaching = coaching
            };
        }

        public async Task<CreatorInsightsDto?> GetCreatorSelfInsightsAsync(int userId, CancellationToken ct = default)
        {
            var profile = await _db.CreatorProfiles.AsNoTracking().FirstOrDefaultAsync(p => p.UserId == userId, ct);
            if (profile == null) return null;

            var channel = await _db.CreatorChannels.AsNoTracking()
                .FirstOrDefaultAsync(c => c.CreatorProfileId == profile.CreatorProfileId, ct);
            if (channel == null)
            {
                return new CreatorInsightsDto
                {
                    HealthScorecard = new CreatorHealthScorecardDto
                    {
                        CreatorId = profile.CreatorProfileId,
                        CompositeScore = 0,
                        WhyExplanation = "Link your channel to unlock health and coaching analytics.",
                        CalculatedAt = DateTime.UtcNow
                    },
                    AudienceQuality = new AudienceQualityDto
                    {
                        Explanation = "No channel linked yet, so audience quality cannot be estimated."
                    },
                    Coaching = new CreatorCoachingDto
                    {
                        WeeklyActionList = new List<string>
                        {
                            "Link your YouTube channel.",
                            "Add category and language in your profile.",
                            "Publish at least 3 videos to generate first insights."
                        }
                    }
                };
            }

            var videos = await _db.ChannelVideos.AsNoTracking()
                .Where(v => v.ChannelId == channel.ChannelId)
                .OrderByDescending(v => v.PublishedAt)
                .Take(40)
                .Select(v => new Video
                {
                    VideoId = v.YoutubeVideoId,
                    Title = v.Title,
                    Description = v.Description,
                    Tags = v.Tags,
                    ViewCount = v.ViewCount,
                    LikeCount = v.LikeCount,
                    CommentCount = v.CommentCount,
                    PublishedAt = v.PublishedAt,
                    EngagementRate = v.ViewCount > 0 ? Math.Round((double)(v.LikeCount + v.CommentCount) / v.ViewCount, 6) : 0,
                })
                .ToListAsync(ct);

            var pseudoCreator = new Creator
            {
                CreatorId = profile.CreatorProfileId,
                ChannelName = channel.ChannelName,
                Category = profile.Category ?? string.Empty,
                Country = profile.Country,
                Language = profile.Language,
                Subscribers = channel.Subscribers,
                TotalViews = channel.TotalViews,
                VideoCount = channel.VideoCount,
                EngagementRate = channel.EngagementRate,
            };

            var health = BuildHealth(pseudoCreator, videos, new List<CreatorGrowth>(), profile.Category);
            var audience = BuildAudience(pseudoCreator, videos);
            var coaching = BuildCoaching(videos);

            return new CreatorInsightsDto
            {
                HealthScorecard = health,
                AudienceQuality = audience,
                Coaching = coaching
            };
        }

        public async Task<CreatorBrandFitDto?> GetCreatorBrandFitAsync(
            int creatorId,
            string? brandCategory,
            string? brandCountry,
            string? brandLanguage,
            CancellationToken ct = default)
        {
            var creator = await _db.Creators.AsNoTracking().FirstOrDefaultAsync(c => c.CreatorId == creatorId, ct);
            if (creator == null) return null;

            var videos = await _db.Videos.AsNoTracking()
                .Where(v => v.CreatorId == creatorId)
                .OrderByDescending(v => v.PublishedAt)
                .Take(30)
                .ToListAsync(ct);

            return BuildFit(creator, videos, brandCategory, brandCountry, brandLanguage);
        }

        public async Task<CampaignOutcomeAnalyticsDto?> GetCampaignOutcomeAnalyticsAsync(int campaignId, CancellationToken ct = default)
        {
            var campaign = await _db.Campaigns.AsNoTracking().FirstOrDefaultAsync(c => c.CampaignId == campaignId, ct);
            if (campaign == null) return null;

            var contributors = await _db.MatchResults.AsNoTracking()
                .Where(m => m.CampaignId == campaignId)
                .Join(_db.Influencers.AsNoTracking(), m => m.InfluencerId, i => i.InfluencerId, (m, i) => new
                {
                    i.InfluencerId,
                    Name = i.User.Name,
                    Reach = (long)i.Followers,
                    i.EngagementRate,
                    MatchScore = m.Score
                })
                .ToListAsync(ct);

            var reach = contributors.Sum(x => x.Reach);
            var engagedViews = contributors.Sum(x => x.Reach * Math.Max(0.01, x.EngagementRate) * (0.45 + (0.55 * Math.Clamp(x.MatchScore, 0, 1))));
            var engagementRate = reach > 0 ? engagedViews / reach : 0;

            var budget = (double)campaign.Budget;
            var cpm = reach > 0 ? budget / (reach / 1000.0) : 0;
            var cpe = engagedViews > 0 ? budget / engagedViews : 0;
            var cpcLike = engagedViews > 0 ? budget / Math.Max(1, engagedViews * 0.08) : 0;

            var avgContribution = contributors.Count > 0 ? engagedViews / contributors.Count : 0;
            var rows = contributors.Select(x =>
            {
                var contribution = x.Reach * Math.Max(0.01, x.EngagementRate) * (0.45 + (0.55 * Math.Clamp(x.MatchScore, 0, 1)));
                var tag = contribution > avgContribution * 1.2
                    ? "Overperformer"
                    : contribution < avgContribution * 0.8
                        ? "Underperformer"
                        : "Baseline";

                return new CreatorContributionDto
                {
                    CreatorId = x.InfluencerId,
                    CreatorName = x.Name,
                    Reach = x.Reach,
                    EngagedViews = Math.Round(contribution, 0),
                    ContributionPercent = engagedViews > 0 ? Math.Round((contribution / engagedViews) * 100, 2) : 0,
                    PerformanceTag = tag,
                };
            }).OrderByDescending(x => x.ContributionPercent).ToList();

            var result = new CampaignOutcomeAnalyticsDto
            {
                CampaignId = campaignId,
                Reach = reach,
                EngagedViews = Math.Round(engagedViews, 0),
                EngagementRate = Math.Round(engagementRate, 4),
                Cpm = Math.Round(cpm, 2),
                Cpe = Math.Round(cpe, 4),
                CpcLikeProxy = Math.Round(cpcLike, 4),
                OverperformerCount = rows.Count(x => x.PerformanceTag == "Overperformer"),
                UnderperformerCount = rows.Count(x => x.PerformanceTag == "Underperformer"),
                CreatorContributions = rows,
                CalculatedAt = DateTime.UtcNow,
            };

            await UpsertCampaignSnapshotAsync(campaign, result, null, ct);
            return result;
        }

        public async Task<PreCampaignForecastDto?> GetPreCampaignForecastAsync(int campaignId, decimal? budgetOverride = null, CancellationToken ct = default)
        {
            var campaign = await _db.Campaigns.AsNoTracking().FirstOrDefaultAsync(c => c.CampaignId == campaignId, ct);
            if (campaign == null) return null;

            var outcome = await GetCampaignOutcomeAnalyticsAsync(campaignId, ct);
            if (outcome == null) return null;

            var budget = (double)(budgetOverride ?? campaign.Budget);
            var confidence = Math.Clamp(0.35 + (outcome.CreatorContributions.Count * 0.08), 0.2, 0.95);
            var confidenceTier = confidence >= 0.75 ? "High" : confidence >= 0.45 ? "Medium" : "Low";

            var expectedViews = Math.Max(100, outcome.EngagedViews * 2.8);
            var lowViews = expectedViews * 0.7;
            var highViews = expectedViews * 1.35;

            var expectedEng = expectedViews * Math.Max(0.015, outcome.EngagementRate);
            var lowEng = expectedEng * 0.7;
            var highEng = expectedEng * 1.35;

            var scenarios = new List<ForecastScenarioDto>
            {
                BuildScenario("low", lowViews, lowEng, budget),
                BuildScenario("expected", expectedViews, expectedEng, budget),
                BuildScenario("high", highViews, highEng, budget),
            };

            var forecast = new PreCampaignForecastDto
            {
                CampaignId = campaignId,
                EstimatedViewsLow = Math.Round(lowViews, 0),
                EstimatedViewsExpected = Math.Round(expectedViews, 0),
                EstimatedViewsHigh = Math.Round(highViews, 0),
                ExpectedEngagementLow = Math.Round(lowEng, 0),
                ExpectedEngagementExpected = Math.Round(expectedEng, 0),
                ExpectedEngagementHigh = Math.Round(highEng, 0),
                ConfidenceScore = Math.Round(confidence, 3),
                ConfidenceTier = confidenceTier,
                BudgetScenarios = scenarios,
                CalculatedAt = DateTime.UtcNow,
            };

            await UpsertCampaignSnapshotAsync(campaign, outcome, forecast, ct);
            return forecast;
        }

        private static ForecastScenarioDto BuildScenario(string name, double views, double engagements, double budget)
        {
            return new ForecastScenarioDto
            {
                Name = name,
                EstimatedViews = Math.Round(views, 0),
                EstimatedEngagements = Math.Round(engagements, 0),
                EstimatedCpm = views > 0 ? Math.Round(budget / (views / 1000.0), 2) : 0,
                EstimatedCpe = engagements > 0 ? Math.Round(budget / engagements, 4) : 0,
            };
        }

        private CreatorHealthScorecardDto BuildHealth(Creator creator, List<Video> videos, List<CreatorGrowth> growth, string? brandCategory)
        {
            var viewSeries = videos.Select(v => (double)v.ViewCount).Where(v => v > 0).ToList();
            var avgViews = viewSeries.Count > 0 ? viewSeries.Average() : Math.Max(1, creator.AvgViews);
            var stdViews = viewSeries.Count > 1 ? StdDev(viewSeries) : avgViews * 0.15;
            var cv = avgViews > 0 ? stdViews / avgViews : 1;

            var consistency = Clamp100(100 - (cv * 55));
            var engQuality = Clamp100((creator.EngagementRate * 100 / 6.0) * 100);
            var growthStability = BuildGrowthStability(growth);
            var relevance = BuildCategoryRelevance(creator.Category, brandCategory);
            var safety = BuildBrandSafety(videos);

            var trend = BuildTrend(videos);
            var composite = Math.Round((consistency * 0.22) + (engQuality * 0.24) + (growthStability * 0.18) + (relevance * 0.16) + (safety * 0.20), 2);

            return new CreatorHealthScorecardDto
            {
                CreatorId = creator.CreatorId,
                CompositeScore = composite,
                ConsistencyScore = Math.Round(consistency, 2),
                EngagementQualityScore = Math.Round(engQuality, 2),
                GrowthStabilityScore = Math.Round(growthStability, 2),
                ContentRelevanceScore = Math.Round(relevance, 2),
                BrandSafetyScore = Math.Round(safety, 2),
                Trend = trend,
                WhyExplanation = BuildWhyExplanation(consistency, engQuality, growthStability, relevance, safety, trend),
                CalculatedAt = DateTime.UtcNow,
            };
        }

        private AudienceQualityDto BuildAudience(Creator creator, List<Video> videos)
        {
            if (!videos.Any())
            {
                return new AudienceQualityDto
                {
                    SuspiciousEngagementRatio = 0,
                    LikeCommentViewConsistencyScore = 50,
                    EngagementVolatilityFlag = false,
                    EngagementVolatilityScore = 40,
                    ReusedCommentPatternScore = 50,
                    Explanation = "No recent videos available; audience quality confidence is low."
                };
            }

            var suspiciousCount = videos.Count(v =>
            {
                var view = Math.Max(1, v.ViewCount);
                var likeRatio = (double)v.LikeCount / view;
                var commentRatio = (double)v.CommentCount / view;
                var er = (double)(v.LikeCount + v.CommentCount) / view;
                return likeRatio > 0.20 || commentRatio > 0.08 || (er < 0.002 && view > 2000);
            });

            var suspiciousRatio = Math.Round((double)suspiciousCount / videos.Count, 4);

            var engagementRatios = videos.Select(v =>
            {
                var view = Math.Max(1, v.ViewCount);
                return (double)(v.LikeCount + v.CommentCount) / view;
            }).ToList();

            var erStd = StdDev(engagementRatios);
            var volatilityScore = Clamp100(100 - (erStd * 1000));
            var volatilityFlag = erStd > 0.03;

            var consistencyRatios = videos.Select(v =>
            {
                var likes = Math.Max(1, v.LikeCount);
                return (double)v.CommentCount / likes;
            }).ToList();
            var consistencyStd = StdDev(consistencyRatios);
            var likeCommentConsistency = Clamp100(100 - (consistencyStd * 300));

            var textCorpus = string.Join(" ", videos.Select(v => $"{v.Title} {v.Description}"));
            var repeatedPhraseScore = DetectRepeatedPatternScore(textCorpus);

            return new AudienceQualityDto
            {
                SuspiciousEngagementRatio = suspiciousRatio,
                LikeCommentViewConsistencyScore = Math.Round(likeCommentConsistency, 2),
                EngagementVolatilityFlag = volatilityFlag,
                EngagementVolatilityScore = Math.Round(volatilityScore, 2),
                ReusedCommentPatternScore = Math.Round(repeatedPhraseScore, 2),
                Explanation = BuildAudienceExplanation(suspiciousRatio, volatilityFlag, likeCommentConsistency, repeatedPhraseScore)
            };
        }

        private CreatorBrandFitDto BuildFit(Creator creator, List<Video> videos, string? brandCategory, string? brandCountry, string? brandLanguage)
        {
            var categoryFit = BuildCategoryRelevance(creator.Category, brandCategory);

            var geoScore = 50.0;
            if (!string.IsNullOrWhiteSpace(brandCountry) && !string.IsNullOrWhiteSpace(creator.Country))
            {
                geoScore += creator.Country.Equals(brandCountry, StringComparison.OrdinalIgnoreCase) ? 35 : -10;
            }
            if (!string.IsNullOrWhiteSpace(brandLanguage) && !string.IsNullOrWhiteSpace(creator.Language))
            {
                geoScore += creator.Language.Contains(brandLanguage, StringComparison.OrdinalIgnoreCase) ? 20 : -8;
            }
            geoScore = Clamp100(geoScore);

            var historicalPerf = Clamp100(((creator.EngagementRate * 100 / 5.0) * 60) + (Math.Log10(Math.Max(1000, creator.AvgViews + 1)) * 15));
            var safety = BuildBrandSafety(videos);

            var overall = Math.Round((categoryFit * 0.35) + (geoScore * 0.25) + (historicalPerf * 0.25) + (safety * 0.15), 2);

            return new CreatorBrandFitDto
            {
                CreatorId = creator.CreatorId,
                BrandCategory = brandCategory ?? "general",
                BrandCountry = brandCountry,
                BrandLanguage = brandLanguage,
                CategoryFitScore = Math.Round(categoryFit, 2),
                LanguageGeoFitScore = Math.Round(geoScore, 2),
                HistoricalPerformanceFitScore = Math.Round(historicalPerf, 2),
                BrandSafetyFitScore = Math.Round(safety, 2),
                OverallFitScore = overall,
                Explanation = $"Fit is driven by category ({categoryFit:F0}), geo/language ({geoScore:F0}), historical performance ({historicalPerf:F0}), and safety ({safety:F0})."
            };
        }

        private CreatorCoachingDto BuildCoaching(List<Video> videos)
        {
            if (!videos.Any())
            {
                return new CreatorCoachingDto
                {
                    BestPostingWindow = "Insufficient data",
                    RetentionProxySuggestions = new List<string>
                    {
                        "Post at least 3 videos this week so the model can detect timing patterns.",
                        "Use stronger hooks in first 15 seconds and track audience drop-off comments.",
                    },
                    WeeklyActionList = new List<string>
                    {
                        "Publish 3 niche-consistent videos.",
                        "Test one short and one long format.",
                        "Add clear CTA in title and pinned comment.",
                    }
                };
            }

            var byHour = videos.GroupBy(v => v.PublishedAt.Hour)
                .Select(g => new
                {
                    Hour = g.Key,
                    AvgEng = g.Average(v => v.EngagementRate > 0 ? v.EngagementRate : (double)(v.LikeCount + v.CommentCount) / Math.Max(1, v.ViewCount))
                })
                .OrderByDescending(x => x.AvgEng)
                .ToList();

            var bestHour = byHour.FirstOrDefault()?.Hour ?? 18;
            var nextHour = (bestHour + 2) % 24;

            var formatPerformance = videos
                .GroupBy(v => DetectFormat(v.Title, v.Description))
                .Select(g => new ContentFormatPerformanceDto
                {
                    Format = g.Key,
                    AvgViews = Math.Round(g.Average(v => (double)v.ViewCount), 0),
                    AvgEngagementRate = Math.Round(g.Average(v => v.EngagementRate > 0 ? v.EngagementRate : (double)(v.LikeCount + v.CommentCount) / Math.Max(1, v.ViewCount)), 4),
                })
                .OrderByDescending(x => x.AvgEngagementRate)
                .Take(4)
                .ToList();

            var suggestions = new List<string>
            {
                "Increase first-30-second value delivery to improve retention proxy.",
                "Use stronger title-keyword alignment with thumbnail to reduce bounce.",
                "Keep mid-video pattern interrupts every 25-40 seconds for long formats.",
            };

            var actions = new List<string>
            {
                $"Publish in the {bestHour:00}:00-{nextHour:00}:00 window for the next 7 days.",
                "Double down on your top format by engagement while testing one contrarian format.",
                "Track comments sentiment and pin one conversion-oriented CTA on each upload.",
            };

            return new CreatorCoachingDto
            {
                BestPostingWindow = $"{bestHour:00}:00 - {nextHour:00}:00 local",
                ContentFormatPerformance = formatPerformance,
                RetentionProxySuggestions = suggestions,
                WeeklyActionList = actions,
            };
        }

        private static string DetectFormat(string? title, string? description)
        {
            var t = $"{title} {description}".ToLowerInvariant();
            if (t.Contains("short") || t.Contains("#shorts")) return "Short";
            if (t.Contains("live") || t.Contains("stream")) return "Live";
            if (t.Contains("review") || t.Contains("vs")) return "Review";
            if (t.Contains("tutorial") || t.Contains("how to")) return "Tutorial";
            return "Standard";
        }

        private static CreatorTrendDto BuildTrend(List<Video> videos)
        {
            var ordered = videos.OrderByDescending(v => v.PublishedAt).ToList();
            var last7 = ordered.Take(7).Select(v => (double)v.ViewCount).ToList();
            var prev7 = ordered.Skip(7).Take(7).Select(v => (double)v.ViewCount).ToList();
            var last30 = ordered.Take(30).Select(v => (double)v.ViewCount).ToList();
            var prev30 = ordered.Skip(30).Take(30).Select(v => (double)v.ViewCount).ToList();

            var d7 = PercentDelta(last7, prev7);
            var d30 = PercentDelta(last30, prev30);

            return new CreatorTrendDto
            {
                Trend7d = ToTrend(d7),
                Trend30d = ToTrend(d30),
                Delta7dPercent = Math.Round(d7, 2),
                Delta30dPercent = Math.Round(d30, 2),
            };
        }

        private static double PercentDelta(List<double> current, List<double> previous)
        {
            var curAvg = current.Count > 0 ? current.Average() : 0;
            var prevAvg = previous.Count > 0 ? previous.Average() : curAvg;
            if (prevAvg <= 0) return 0;
            return ((curAvg - prevAvg) / prevAvg) * 100;
        }

        private static string ToTrend(double delta)
        {
            if (delta > 8) return "up";
            if (delta < -8) return "down";
            return "flat";
        }

        private static double BuildGrowthStability(List<CreatorGrowth> growth)
        {
            if (growth.Count < 4) return 55;

            var ordered = growth.OrderBy(g => g.RecordedAt).ToList();
            var deltas = new List<double>();
            for (var i = 1; i < ordered.Count; i++)
            {
                var prev = Math.Max(1, ordered[i - 1].Subscribers);
                deltas.Add((ordered[i].Subscribers - ordered[i - 1].Subscribers) / (double)prev);
            }

            var std = StdDev(deltas);
            return Clamp100(100 - (std * 500));
        }

        private static double BuildCategoryRelevance(string? creatorCategory, string? brandCategory)
        {
            if (string.IsNullOrWhiteSpace(brandCategory)) return 70;
            if (string.IsNullOrWhiteSpace(creatorCategory)) return 40;

            if (creatorCategory.Equals(brandCategory, StringComparison.OrdinalIgnoreCase)) return 100;
            if (creatorCategory.Contains(brandCategory, StringComparison.OrdinalIgnoreCase) || brandCategory.Contains(creatorCategory, StringComparison.OrdinalIgnoreCase)) return 78;
            return 35;
        }

        private static double BuildBrandSafety(List<Video> videos)
        {
            if (!videos.Any()) return 60;
            var profanityHits = videos.Count(v => ContainsUnsafeTokens(v.Title) || ContainsUnsafeTokens(v.Description));
            var ratio = (double)profanityHits / videos.Count;
            return Clamp100(100 - (ratio * 100));
        }

        private static bool ContainsUnsafeTokens(string? value)
        {
            if (string.IsNullOrWhiteSpace(value)) return false;
            var t = value.ToLowerInvariant();
            return t.Contains("violence") || t.Contains("explicit") || t.Contains("abuse") || t.Contains("hate");
        }

        private static string BuildWhyExplanation(double consistency, double engagement, double growth, double relevance, double safety, CreatorTrendDto trend)
        {
            var strong = new List<string>();
            var weak = new List<string>();

            if (consistency >= 70) strong.Add("consistent views"); else weak.Add("view consistency");
            if (engagement >= 70) strong.Add("healthy engagement quality"); else weak.Add("engagement quality");
            if (growth >= 65) strong.Add("stable growth"); else weak.Add("growth stability");
            if (relevance >= 70) strong.Add("strong category relevance"); else weak.Add("category alignment");
            if (safety >= 75) strong.Add("good brand safety"); else weak.Add("brand safety signals");

            var trendPart = $"Trends are {trend.Trend7d} (7d) and {trend.Trend30d} (30d).";
            return $"Strengths: {string.Join(", ", strong)}. Improve: {string.Join(", ", weak)}. {trendPart}";
        }

        private static string BuildAudienceExplanation(double suspiciousRatio, bool volatilityFlag, double consistencyScore, double reusedPatternScore)
        {
            var risk = suspiciousRatio > 0.35 || volatilityFlag || consistencyScore < 45 || reusedPatternScore > 70;
            return risk
                ? "Audience quality has medium-to-high risk indicators; monitor unusual engagement spikes and repetitive interaction patterns."
                : "Audience quality signals are generally healthy with no major authenticity flags.";
        }

        private static double DetectRepeatedPatternScore(string corpus)
        {
            if (string.IsNullOrWhiteSpace(corpus)) return 25;

            var words = corpus.ToLowerInvariant()
                .Split(new[] { ' ', '\n', '\r', '\t', ',', '.', '!', '?', ':', ';', '-', '_', '/' }, StringSplitOptions.RemoveEmptyEntries)
                .Where(w => w.Length >= 4)
                .Take(800)
                .ToList();

            if (words.Count < 20) return 20;

            var dupRatio = words.GroupBy(w => w)
                .Select(g => g.Count())
                .Where(c => c >= 4)
                .Sum() / (double)words.Count;

            return Clamp100(dupRatio * 100);
        }

        private async Task UpsertCreatorHealthSnapshotAsync(
            int creatorId,
            CreatorHealthScorecardDto health,
            AudienceQualityDto audience,
            CreatorCoachingDto coaching,
            CancellationToken ct)
        {
            var existing = await _db.CreatorHealthSnapshots.FirstOrDefaultAsync(x => x.CreatorId == creatorId, ct);
            if (existing == null)
            {
                existing = new CreatorHealthSnapshot { CreatorId = creatorId };
                _db.CreatorHealthSnapshots.Add(existing);
            }

            existing.CompositeScore = health.CompositeScore;
            existing.ConsistencyScore = health.ConsistencyScore;
            existing.EngagementQualityScore = health.EngagementQualityScore;
            existing.GrowthStabilityScore = health.GrowthStabilityScore;
            existing.ContentRelevanceScore = health.ContentRelevanceScore;
            existing.BrandSafetyScore = health.BrandSafetyScore;
            existing.Trend7d = health.Trend.Trend7d;
            existing.Trend30d = health.Trend.Trend30d;
            existing.Delta7dPercent = health.Trend.Delta7dPercent;
            existing.Delta30dPercent = health.Trend.Delta30dPercent;
            existing.WhyExplanation = health.WhyExplanation;

            existing.SuspiciousEngagementRatio = audience.SuspiciousEngagementRatio;
            existing.LikeCommentViewConsistencyScore = audience.LikeCommentViewConsistencyScore;
            existing.EngagementVolatilityFlag = audience.EngagementVolatilityFlag;
            existing.EngagementVolatilityScore = audience.EngagementVolatilityScore;
            existing.ReusedCommentPatternScore = audience.ReusedCommentPatternScore;
            existing.AudienceExplanation = audience.Explanation;

            existing.BestPostingWindow = coaching.BestPostingWindow;
            existing.ContentFormatPerformanceJson = JsonSerializer.Serialize(coaching.ContentFormatPerformance);
            existing.RetentionSuggestionsJson = JsonSerializer.Serialize(coaching.RetentionProxySuggestions);
            existing.WeeklyActionsJson = JsonSerializer.Serialize(coaching.WeeklyActionList);
            existing.CalculatedAt = DateTime.UtcNow;

            await _db.SaveChangesAsync(ct);
        }

        private async Task UpsertCampaignSnapshotAsync(
            Campaign campaign,
            CampaignOutcomeAnalyticsDto outcome,
            PreCampaignForecastDto? forecast,
            CancellationToken ct)
        {
            var existing = await _db.CampaignAnalyticsSnapshots.FirstOrDefaultAsync(x => x.CampaignId == campaign.CampaignId, ct);
            if (existing == null)
            {
                existing = new CampaignAnalyticsSnapshot { CampaignId = campaign.CampaignId };
                _db.CampaignAnalyticsSnapshots.Add(existing);
            }

            existing.Reach = outcome.Reach;
            existing.EngagedViews = outcome.EngagedViews;
            existing.EngagementRate = outcome.EngagementRate;
            existing.Cpm = outcome.Cpm;
            existing.Cpe = outcome.Cpe;
            existing.CpcLikeProxy = outcome.CpcLikeProxy;
            existing.OverperformerCount = outcome.OverperformerCount;
            existing.UnderperformerCount = outcome.UnderperformerCount;
            existing.CreatorContributionsJson = JsonSerializer.Serialize(outcome.CreatorContributions);

            if (forecast != null)
            {
                existing.EstimatedViewsLow = forecast.EstimatedViewsLow;
                existing.EstimatedViewsExpected = forecast.EstimatedViewsExpected;
                existing.EstimatedViewsHigh = forecast.EstimatedViewsHigh;
                existing.ExpectedEngagementLow = forecast.ExpectedEngagementLow;
                existing.ExpectedEngagementExpected = forecast.ExpectedEngagementExpected;
                existing.ExpectedEngagementHigh = forecast.ExpectedEngagementHigh;
                existing.ConfidenceScore = forecast.ConfidenceScore;
                existing.ConfidenceTier = forecast.ConfidenceTier;
                existing.BudgetScenariosJson = JsonSerializer.Serialize(forecast.BudgetScenarios);
            }

            existing.CalculatedAt = DateTime.UtcNow;
            await _db.SaveChangesAsync(ct);
        }

        private static double StdDev(List<double> values)
        {
            if (values.Count <= 1) return 0;
            var avg = values.Average();
            var sum = values.Sum(v => Math.Pow(v - avg, 2));
            return Math.Sqrt(sum / values.Count);
        }

        private static double Clamp100(double value) => Math.Max(0, Math.Min(100, value));
    }
}
