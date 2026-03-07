using System;
using System.Collections.Generic;

namespace InfluencerMatch.Application.DTOs
{
    // ── Feature 1: Rising Creator ─────────────────────────────────────────────

    public class RisingCreatorDto
    {
        public int      CreatorId           { get; set; }
        public string   ChannelName         { get; set; } = string.Empty;
        public string   Platform            { get; set; } = string.Empty;
        public string   Category            { get; set; } = string.Empty;
        public string   Country             { get; set; } = string.Empty;
        public long     Subscribers         { get; set; }
        public double   GrowthRate          { get; set; }   // 0.12 = 12 %
        public string   GrowthCategory      { get; set; } = string.Empty;
        public long     SubscriberDelta     { get; set; }
        public double   EngagementRate      { get; set; }
        public DateTime CalculatedAt        { get; set; }
    }

    // ── Feature 2: Brand Opportunity ──────────────────────────────────────────

    public class BrandOpportunityRequestDto
    {
        public string? BrandCategory  { get; set; }
        public string? Country        { get; set; }        // optional country filter, e.g. "IN"
        public int     TopN           { get; set; } = 20;
    }

    public class BrandOpportunityDto
    {
        public int     CreatorId        { get; set; }
        public string  ChannelName      { get; set; } = string.Empty;
        public string  Platform         { get; set; } = string.Empty;
        public string  Category         { get; set; } = string.Empty;
        public string  Country          { get; set; } = string.Empty;
        public long    Subscribers      { get; set; }
        public double  EngagementRate   { get; set; }
        public double  GrowthRate       { get; set; }
        public double  OpportunityScore { get; set; }    // composite recommendation score
        public string  GrowthCategory   { get; set; } = string.Empty;
        public double  EstimatedPrice   { get; set; }
    }

    // ── Feature 3: Campaign Performance Prediction ────────────────────────────

    public class CampaignPredictionDto
    {
        public int    CreatorId          { get; set; }
        public string ChannelName        { get; set; } = string.Empty;
        public double AverageViews       { get; set; }
        public double EngagementRate     { get; set; }

        /// <summary>AverageViews × EngagementMultiplier</summary>
        public double ExpectedViews      { get; set; }

        /// <summary>ExpectedViews × EngagementRate</summary>
        public double ExpectedEngagement { get; set; }

        /// <summary>0 – 1. Based on data freshness and data point count.</summary>
        public double ConfidenceScore    { get; set; }

        /// <summary>Human-readable confidence tier: High / Medium / Low</summary>
        public string ConfidenceTier     { get; set; } = string.Empty;

        /// <summary>Multiplier applied to AverageViews (accounts for campaign boost).</summary>
        public double EngagementMultiplier { get; set; }
    }

    // ── Feature 4: Creator Price Estimation ───────────────────────────────────

    public class CreatorPriceDto
    {
        public int    CreatorId           { get; set; }
        public string ChannelName         { get; set; } = string.Empty;
        public double AverageViews        { get; set; }
        public double EngagementRate      { get; set; }

        /// <summary>AverageViews × 0.02 (base) adjusted for engagement premium.</summary>
        public double EstimatedPriceUSD   { get; set; }

        /// <summary>Estimated price in INR (× 83).</summary>
        public double EstimatedPriceINR   { get; set; }

        /// <summary>Lower bound of price range (−30 %).</summary>
        public double PriceRangeLow       { get; set; }

        /// <summary>Upper bound of price range (+50 %).</summary>
        public double PriceRangeHigh      { get; set; }

        /// <summary>Brief explanation of the pricing factors.</summary>
        public string PricingRationale    { get; set; } = string.Empty;
    }
}
