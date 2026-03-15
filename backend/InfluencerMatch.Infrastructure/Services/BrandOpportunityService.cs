using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using InfluencerMatch.Application.DTOs;
using InfluencerMatch.Application.Interfaces;
using InfluencerMatch.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace InfluencerMatch.Infrastructure.Services
{
    /// <summary>
    /// Feature 2 — Brand Opportunity Finder.
    ///
    /// OpportunityScore = 0.5 × norm(EngagementRate)
    ///                  + 0.3 × norm(GrowthRate)
    ///                  + 0.2 × norm(Subscribers)
    ///
    /// Normalisation ceilings (clamp then scale to 0-100):
    ///   EngagementRate  → max = 0.10 (10 %)
    ///   GrowthRate      → max = 0.10 (10 % monthly)
    ///   Subscribers     → max = 10,000,000
    /// </summary>
    public class BrandOpportunityService : IBrandOpportunityService
    {
        private readonly ApplicationDbContext _db;
        private readonly ILogger<BrandOpportunityService> _logger;

        private const double EngMax  = 0.10;
        private const double GrowMax = 0.10;
        private const double SubsMax = 10_000_000.0;

        public BrandOpportunityService(ApplicationDbContext db, ILogger<BrandOpportunityService> logger)
        {
            _db    = db;
            _logger = logger;
        }

        public async Task<List<BrandOpportunityDto>> FindOpportunitiesAsync(BrandOpportunityRequestDto request)
        {
            int topN = Math.Clamp(request.TopN, 1, 200);

            _logger.LogInformation(
                "BrandOpportunityService: category={Cat}, country={Ctry}, topN={N}",
                request.BrandCategory, request.Country, topN);

            // Load matching creators + analytics + growth scores
            var creatorsQuery = _db.Creators.AsNoTracking()
                .Where(c => c.UserId.HasValue && c.User != null)
                .Where(c => string.IsNullOrWhiteSpace(request.BrandCategory) || c.Category == request.BrandCategory)
                .Where(c => string.IsNullOrWhiteSpace(request.Country)       || c.Country   == request.Country);

            var creators  = await creatorsQuery.ToListAsync();
            var ids       = creators.Select(c => c.CreatorId).ToList();

            var analyticsMap = await _db.CreatorAnalytics.AsNoTracking()
                .Where(a => ids.Contains(a.CreatorId))
                .ToDictionaryAsync(a => a.CreatorId);

            var growthMap = await _db.CreatorGrowthScores.AsNoTracking()
                .Where(g => ids.Contains(g.CreatorId))
                .ToDictionaryAsync(g => g.CreatorId);

            // Build raw scores
            var withScores = creators.Select(creator =>
            {
                analyticsMap.TryGetValue(creator.CreatorId, out var a);
                growthMap.TryGetValue(creator.CreatorId,    out var g);

                double engRate    = EngagementRateEstimator.EstimateOrStored(
                    a?.EngagementRate,
                    creator.Subscribers,
                    creator.TotalViews,
                    creator.VideoCount,
                    a?.AvgViews);
                double growthRate = g?.GrowthRate     ?? 0;
                long   subs       = creator.Subscribers;
                double avgViews   = a?.AvgViews   ?? 0;

                double normEng  = Norm(engRate,    EngMax);
                double normGrow = Norm(growthRate, GrowMax);
                double normSubs = Norm(subs,        SubsMax);

                double oppScore = (0.5 * normEng) + (0.3 * normGrow) + (0.2 * normSubs);
                int audienceFitScore = CalculateAudienceFitScore(request, creator, engRate, growthRate, subs);
                var fitSignals = BuildFitSignals(request, creator, engRate, growthRate, subs, estimatedPriceUsd: 0);

                // Price estimate (same formula as CampaignPredictionService)
                double basePrice        = avgViews * 0.02;
                double engPremium       = Math.Min(1.0 + engRate / 0.05, 2.0);
                double estimatedPriceUSD = basePrice * engPremium;
                fitSignals = BuildFitSignals(request, creator, engRate, growthRate, subs, estimatedPriceUSD);
                var suggestedActivation = BuildSuggestedActivation(engRate, growthRate, subs);
                var riskNote = BuildRiskNote(engRate, growthRate, subs);
                var fitNarrative = BuildFitNarrative(request, creator, fitSignals, suggestedActivation, riskNote);
                var readinessLevel = BuildReadinessLevel(oppScore, audienceFitScore, engRate, growthRate);
                var recommendedCampaignGoal = BuildRecommendedCampaignGoal(engRate, growthRate, subs);
                var riskScore = BuildRiskScore(engRate, growthRate, subs);

                return new BrandOpportunityDto
                {
                    CreatorId        = creator.CreatorId,
                    ChannelName      = creator.ChannelName ?? string.Empty,
                    Platform         = creator.Platform,
                    Category         = creator.Category   ?? string.Empty,
                    Country          = creator.Country    ?? string.Empty,
                    Subscribers      = subs,
                    EngagementRate   = engRate,
                    GrowthRate       = growthRate,
                    GrowthCategory   = g?.GrowthCategory ?? "Unknown",
                    OpportunityScore = Math.Round(oppScore, 4),
                    EstimatedPrice   = Math.Round(estimatedPriceUSD, 2),
                    AudienceFitScore = audienceFitScore,
                    AiFitNarrative   = fitNarrative,
                    SuggestedActivation = suggestedActivation,
                    RiskNote         = riskNote,
                    AiFitSignals     = fitSignals,
                    AiReadinessLevel = readinessLevel,
                    AiRecommendedCampaignGoal = recommendedCampaignGoal,
                    AiRiskScore = riskScore
                };
            })
            .OrderByDescending(x => x.OpportunityScore)
            .Take(topN)
            .ToList();

            return withScores;
        }

        // ── helpers ──────────────────────────────────────────────────────────

        private static double Norm(double value, double max)
            => max <= 0 ? 0 : Math.Clamp(value / max, 0.0, 1.0);

        private static double Norm(long value, double max)
            => Norm((double)value, max);

        private static int CalculateAudienceFitScore(BrandOpportunityRequestDto request, dynamic creator, double engagementRate, double growthRate, long subscribers)
        {
            var score = 48;

            if (!string.IsNullOrWhiteSpace(request.Country)
                && string.Equals(request.Country, creator.Country, StringComparison.OrdinalIgnoreCase))
            {
                score += 12;
            }

            if (!string.IsNullOrWhiteSpace(request.BrandCategory)
                && string.Equals(request.BrandCategory, creator.Category, StringComparison.OrdinalIgnoreCase))
            {
                score += 14;
            }

            if (engagementRate >= 0.05) score += 14;
            else if (engagementRate >= 0.03) score += 8;
            else if (engagementRate < 0.015) score -= 8;

            if (growthRate >= 0.05) score += 8;
            else if (growthRate < 0) score -= 10;

            if (subscribers >= 1_000_000) score += 6;
            if (subscribers >= 5_000_000) score += 4;

            return Math.Clamp(score, 35, 95);
        }

        private static List<string> BuildFitSignals(BrandOpportunityRequestDto request, dynamic creator, double engagementRate, double growthRate, long subscribers, double estimatedPriceUsd)
        {
            var signals = new List<string>();

            if (!string.IsNullOrWhiteSpace(request.BrandCategory)
                && string.Equals(request.BrandCategory, creator.Category, StringComparison.OrdinalIgnoreCase))
            {
                signals.Add($"Category aligned with {creator.Category}");
            }

            if (!string.IsNullOrWhiteSpace(request.Country)
                && string.Equals(request.Country, creator.Country, StringComparison.OrdinalIgnoreCase))
            {
                signals.Add($"Geo aligned with {creator.Country}");
            }

            if (engagementRate >= 0.05)
            {
                signals.Add($"High engagement at {engagementRate * 100:F1}%");
            }
            else if (engagementRate >= 0.03)
            {
                signals.Add($"Healthy engagement at {engagementRate * 100:F1}%");
            }
            else
            {
                signals.Add("Reach-led profile better suited to awareness");
            }

            if (growthRate >= 0.05)
            {
                signals.Add($"Fast monthly growth at {growthRate * 100:F1}%");
            }
            else if (growthRate > 0)
            {
                signals.Add($"Positive momentum at {growthRate * 100:F1}% monthly");
            }
            else
            {
                signals.Add("Momentum currently flat or softening");
            }

            if (subscribers >= 5_000_000)
            {
                signals.Add("Mass-reach creator for launch visibility");
            }
            else if (subscribers >= 500_000)
            {
                signals.Add("Mid-scale creator with balanced reach and efficiency");
            }
            else
            {
                signals.Add("Emerging creator with upside pricing efficiency");
            }

            if (estimatedPriceUsd > 0)
            {
                if (estimatedPriceUsd <= 400)
                {
                    signals.Add("Accessible sponsorship entry point");
                }
                else if (estimatedPriceUsd >= 1500)
                {
                    signals.Add("Premium placement likely required");
                }
            }

            return signals.Take(5).ToList();
        }

        private static string BuildSuggestedActivation(double engagementRate, double growthRate, long subscribers)
        {
            if (engagementRate >= 0.05 && growthRate >= 0.03)
            {
                return "Best suited for conversion-led integrations, creator demos, or launch-week call-to-action campaigns.";
            }

            if (subscribers >= 1_000_000 && engagementRate < 0.025)
            {
                return "Best suited for awareness bursts, hero launches, and top-of-funnel reach objectives.";
            }

            if (growthRate >= 0.05)
            {
                return "Best suited for early-entry partnerships before pricing matures with further growth.";
            }

            return "Best suited for balanced brand mentions, creator-led storytelling, and mid-funnel consideration campaigns.";
        }

        private static string BuildRiskNote(double engagementRate, double growthRate, long subscribers)
        {
            if (subscribers >= 1_000_000 && engagementRate < 0.015)
            {
                return "Large audience but interaction efficiency is soft, so benchmark on reach and CPM rather than conversion rate.";
            }

            if (growthRate < 0)
            {
                return "Recent momentum is negative, so validate recent content performance before committing larger budgets.";
            }

            if (engagementRate < 0.02)
            {
                return "Engagement is usable but not elite, so creative quality and CTA clarity will matter more than audience size alone.";
            }

            return "Current engagement and momentum profile suggests manageable execution risk for brand activation.";
        }

        private static string BuildReadinessLevel(double opportunityScore, int audienceFitScore, double engagementRate, double growthRate)
        {
            if (opportunityScore >= 0.72 && audienceFitScore >= 78 && engagementRate >= 0.03 && growthRate >= 0.01)
            {
                return "High";
            }

            if (opportunityScore >= 0.55 && audienceFitScore >= 62)
            {
                return "Medium";
            }

            return "Watch";
        }

        private static string BuildRecommendedCampaignGoal(double engagementRate, double growthRate, long subscribers)
        {
            if (engagementRate >= 0.045)
            {
                return "Conversion";
            }

            if (growthRate >= 0.05)
            {
                return "Community Growth";
            }

            if (subscribers >= 1_000_000)
            {
                return "Awareness";
            }

            return "Consideration";
        }

        private static int BuildRiskScore(double engagementRate, double growthRate, long subscribers)
        {
            var score = 48;

            if (subscribers >= 1_000_000 && engagementRate < 0.015)
            {
                score += 22;
            }

            if (growthRate < 0)
            {
                score += 18;
            }
            else if (growthRate >= 0.05)
            {
                score -= 6;
            }

            if (engagementRate < 0.01)
            {
                score += 18;
            }
            else if (engagementRate >= 0.04)
            {
                score -= 8;
            }

            return Math.Clamp(score, 20, 90);
        }

        private static string BuildFitNarrative(BrandOpportunityRequestDto request, dynamic creator, List<string> fitSignals, string suggestedActivation, string riskNote)
        {
            var categoryLabel = string.IsNullOrWhiteSpace(request.BrandCategory)
                ? "your campaign"
                : request.BrandCategory.Trim();

            var topSignals = fitSignals.Take(3).ToList();
            var signalSummary = topSignals.Count switch
            {
                0 => "baseline opportunity signals are present",
                1 => topSignals[0].ToLowerInvariant(),
                2 => $"{topSignals[0].ToLowerInvariant()} and {topSignals[1].ToLowerInvariant()}",
                _ => $"{topSignals[0].ToLowerInvariant()}, {topSignals[1].ToLowerInvariant()}, and {topSignals[2].ToLowerInvariant()}"
            };

            return $"{creator.ChannelName} is a strong-fit candidate for {categoryLabel} because {signalSummary}. {suggestedActivation} {riskNote}";
        }
    }
}
