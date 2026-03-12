using System;
using System.Collections.Generic;

namespace InfluencerMatch.Application.DTOs
{
    public class CreatorTrendDto
    {
        public string Trend7d { get; set; } = "flat";   // up | flat | down
        public string Trend30d { get; set; } = "flat";  // up | flat | down
        public double Delta7dPercent { get; set; }
        public double Delta30dPercent { get; set; }
    }

    public class CreatorHealthScorecardDto
    {
        public int CreatorId { get; set; }
        public double CompositeScore { get; set; }
        public double ConsistencyScore { get; set; }
        public double EngagementQualityScore { get; set; }
        public double GrowthStabilityScore { get; set; }
        public double ContentRelevanceScore { get; set; }
        public double BrandSafetyScore { get; set; }
        public CreatorTrendDto Trend { get; set; } = new();
        public string WhyExplanation { get; set; } = string.Empty;
        public DateTime CalculatedAt { get; set; }
    }

    public class AudienceQualityDto
    {
        public double SuspiciousEngagementRatio { get; set; }
        public double LikeCommentViewConsistencyScore { get; set; }
        public bool EngagementVolatilityFlag { get; set; }
        public double EngagementVolatilityScore { get; set; }
        public double ReusedCommentPatternScore { get; set; }
        public string Explanation { get; set; } = string.Empty;
    }

    public class CreatorBrandFitDto
    {
        public int CreatorId { get; set; }
        public string BrandCategory { get; set; } = string.Empty;
        public string? BrandCountry { get; set; }
        public string? BrandLanguage { get; set; }
        public double CategoryFitScore { get; set; }
        public double LanguageGeoFitScore { get; set; }
        public double HistoricalPerformanceFitScore { get; set; }
        public double BrandSafetyFitScore { get; set; }
        public double OverallFitScore { get; set; }
        public string Explanation { get; set; } = string.Empty;
    }

    public class ContentFormatPerformanceDto
    {
        public string Format { get; set; } = string.Empty;
        public double AvgViews { get; set; }
        public double AvgEngagementRate { get; set; }
    }

    public class CreatorCoachingDto
    {
        public string BestPostingWindow { get; set; } = string.Empty;
        public List<ContentFormatPerformanceDto> ContentFormatPerformance { get; set; } = new();
        public List<string> RetentionProxySuggestions { get; set; } = new();
        public List<string> WeeklyActionList { get; set; } = new();
    }

    public class CreatorInsightsDto
    {
        public CreatorHealthScorecardDto HealthScorecard { get; set; } = new();
        public AudienceQualityDto AudienceQuality { get; set; } = new();
        public CreatorCoachingDto Coaching { get; set; } = new();
    }

    public class CreatorContributionDto
    {
        public int CreatorId { get; set; }
        public string CreatorName { get; set; } = string.Empty;
        public long Reach { get; set; }
        public double EngagedViews { get; set; }
        public double ContributionPercent { get; set; }
        public string PerformanceTag { get; set; } = string.Empty; // Overperformer | Baseline | Underperformer
    }

    public class CampaignOutcomeAnalyticsDto
    {
        public int CampaignId { get; set; }
        public long Reach { get; set; }
        public double EngagedViews { get; set; }
        public double EngagementRate { get; set; }
        public double Cpm { get; set; }
        public double Cpe { get; set; }
        public double CpcLikeProxy { get; set; }
        public int OverperformerCount { get; set; }
        public int UnderperformerCount { get; set; }
        public List<CreatorContributionDto> CreatorContributions { get; set; } = new();
        public DateTime CalculatedAt { get; set; }
    }

    public class ForecastScenarioDto
    {
        public string Name { get; set; } = string.Empty; // low | expected | high
        public double EstimatedViews { get; set; }
        public double EstimatedEngagements { get; set; }
        public double EstimatedCpm { get; set; }
        public double EstimatedCpe { get; set; }
    }

    public class PreCampaignForecastDto
    {
        public int CampaignId { get; set; }
        public double EstimatedViewsLow { get; set; }
        public double EstimatedViewsExpected { get; set; }
        public double EstimatedViewsHigh { get; set; }
        public double ExpectedEngagementLow { get; set; }
        public double ExpectedEngagementExpected { get; set; }
        public double ExpectedEngagementHigh { get; set; }
        public double ConfidenceScore { get; set; }
        public string ConfidenceTier { get; set; } = string.Empty;
        public List<ForecastScenarioDto> BudgetScenarios { get; set; } = new();
        public DateTime CalculatedAt { get; set; }
    }
}
