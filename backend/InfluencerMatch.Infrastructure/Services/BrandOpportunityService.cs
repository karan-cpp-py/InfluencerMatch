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

                // Price estimate (same formula as CampaignPredictionService)
                double basePrice        = avgViews * 0.02;
                double engPremium       = Math.Min(1.0 + engRate / 0.05, 2.0);
                double estimatedPriceUSD = basePrice * engPremium;

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
                    EstimatedPrice   = Math.Round(estimatedPriceUSD, 2)
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
    }
}
