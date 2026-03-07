using System;
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
    /// Feature 3 — Campaign Performance Prediction
    /// Feature 4 — Creator Price Estimation
    ///
    /// Both features share the same data access pattern so they live in one class.
    ///
    /// ── Campaign prediction ──
    ///   EngagementMultiplier = clamp(1.0 + EngagementRate / 0.05, 1.0, 2.5)
    ///   ExpectedViews        = AverageViews × EngagementMultiplier
    ///   ExpectedEngagement   = ExpectedViews × EngagementRate
    ///   ConfidenceScore      = weighted blend of data-freshness score + snapshot-count score
    ///   ConfidenceTier       = High ≥ 0.7  /  Medium ≥ 0.4  /  Low < 0.4
    ///
    /// ── Price estimation ──
    ///   BasePrice            = AverageViews × 0.02  (USD)
    ///   EngagementPremium    = clamp(1.0 + EngagementRate / 0.05, 1.0, 2.0)
    ///   EstimatedPriceUSD    = BasePrice × EngagementPremium
    ///   EstimatedPriceINR    = EstimatedPriceUSD × 83
    ///   PriceRangeLow        = EstimatedPriceUSD × 0.70
    ///   PriceRangeHigh       = EstimatedPriceUSD × 1.50
    /// </summary>
    public class CampaignPredictionService : ICampaignPredictionService, ICreatorPricingService
    {
        private readonly ApplicationDbContext _db;
        private readonly ILogger<CampaignPredictionService> _logger;

        private const double EngMultiplierCap  = 2.5;
        private const double PricePremiumCap   = 2.0;
        private const double UsdToInr          = 83.0;
        private const double PricePerView      = 0.02;

        public CampaignPredictionService(ApplicationDbContext db, ILogger<CampaignPredictionService> logger)
        {
            _db    = db;
            _logger = logger;
        }

        // ── Feature 3 ────────────────────────────────────────────────────────

        public async Task<CampaignPredictionDto?> PredictAsync(int creatorId)
        {
            var creator = await _db.Creators.AsNoTracking()
                .FirstOrDefaultAsync(c => c.CreatorId == creatorId);
            if (creator == null) return null;

            var analytics = await _db.CreatorAnalytics.AsNoTracking()
                .FirstOrDefaultAsync(a => a.CreatorId == creatorId);
            if (analytics == null) return null;

            double avgViews    = analytics.AvgViews;
            double engRate     = analytics.EngagementRate;

            double engMult         = Math.Clamp(1.0 + engRate / 0.05, 1.0, EngMultiplierCap);
            double expectedViews   = avgViews   * engMult;
            double expectedEng     = expectedViews * engRate;

            // Confidence calculation
            double freshnessScore  = FreshnessScore(analytics.CalculatedAt);
            double snapshotScore   = await SnapshotScoreAsync(creatorId);
            double confidenceScore = (0.6 * freshnessScore) + (0.4 * snapshotScore);

            string tier = confidenceScore >= 0.70 ? "High"
                        : confidenceScore >= 0.40 ? "Medium"
                        :                           "Low";

            _logger.LogDebug("CampaignPrediction: creator={Id}, engMult={M:F2}, confidence={C:F2}", creatorId, engMult, confidenceScore);

            return new CampaignPredictionDto
            {
                CreatorId            = creator.CreatorId,
                ChannelName          = creator.ChannelName ?? string.Empty,
                AverageViews         = avgViews,
                EngagementRate       = engRate,
                ExpectedViews        = Math.Round(expectedViews,   0),
                ExpectedEngagement   = Math.Round(expectedEng,     0),
                EngagementMultiplier = Math.Round(engMult,         4),
                ConfidenceScore      = Math.Round(confidenceScore, 4),
                ConfidenceTier       = tier
            };
        }

        // ── Feature 4 ────────────────────────────────────────────────────────

        public async Task<CreatorPriceDto?> EstimatePriceAsync(int creatorId)
        {
            var creator = await _db.Creators.AsNoTracking()
                .FirstOrDefaultAsync(c => c.CreatorId == creatorId);
            if (creator == null) return null;

            var analytics = await _db.CreatorAnalytics.AsNoTracking()
                .FirstOrDefaultAsync(a => a.CreatorId == creatorId);
            if (analytics == null) return null;

            double avgViews   = analytics.AvgViews;
            double engRate    = analytics.EngagementRate;

            double basePrice        = avgViews * PricePerView;
            double engPremium       = Math.Clamp(1.0 + engRate / 0.05, 1.0, PricePremiumCap);
            double estimatedUsd     = basePrice * engPremium;
            double estimatedInr     = estimatedUsd * UsdToInr;
            double rangeLow         = estimatedUsd * 0.70;
            double rangeHigh        = estimatedUsd * 1.50;

            string rationale = BuildPricingRationale(avgViews, engRate, engPremium, estimatedUsd);

            _logger.LogDebug("CreatorPrice: creator={Id}, USD={USD:F2}, INR={INR:F0}", creatorId, estimatedUsd, estimatedInr);

            return new CreatorPriceDto
            {
                CreatorId          = creator.CreatorId,
                ChannelName        = creator.ChannelName ?? string.Empty,
                AverageViews       = avgViews,
                EngagementRate     = engRate,
                EstimatedPriceUSD  = Math.Round(estimatedUsd, 2),
                EstimatedPriceINR  = Math.Round(estimatedInr, 0),
                PriceRangeLow      = Math.Round(rangeLow,     2),
                PriceRangeHigh     = Math.Round(rangeHigh,    2),
                PricingRationale   = rationale
            };
        }

        // ── private helpers ──────────────────────────────────────────────────

        /// <summary>Score 0-1 based on how recent the analytics snapshot is.</summary>
        private static double FreshnessScore(DateTime calculatedAt)
        {
            double ageDays = (DateTime.UtcNow - calculatedAt).TotalDays;
            if (ageDays <=  7) return 1.0;
            if (ageDays <= 14) return 0.75;
            if (ageDays <= 30) return 0.50;
            if (ageDays <= 60) return 0.25;
            return 0.1;
        }

        /// <summary>Score 0-1 based on number of growth snapshots available.</summary>
        private async Task<double> SnapshotScoreAsync(int creatorId)
        {
            int count = await _db.CreatorGrowth
                .CountAsync(g => g.CreatorId == creatorId);
            if (count >= 10) return 1.0;
            if (count >=  5) return 0.75;
            if (count >=  3) return 0.50;
            if (count >=  1) return 0.25;
            return 0.0;
        }

        private static string BuildPricingRationale(double avgViews, double engRate, double engPremium, double usd)
        {
            string engLabel = engRate >= 0.08 ? "exceptional"
                            : engRate >= 0.05 ? "high"
                            : engRate >= 0.02 ? "moderate"
                            :                   "low";

            return $"Base rate ${PricePerView}/view × {avgViews:N0} avg views = ${avgViews * PricePerView:N0}. " +
                   $"{engLabel.ToUpper()} engagement ({engRate:P1}) adds a {engPremium:F2}× premium. " +
                   $"Final estimate: ${usd:N0} (±30-50%).";
        }
    }
}
