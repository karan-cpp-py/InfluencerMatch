using System;

namespace InfluencerMatch.Domain.Entities
{
    public class CreatorHealthSnapshot
    {
        public int CreatorHealthSnapshotId { get; set; }
        public int CreatorId { get; set; }
        public Creator Creator { get; set; } = null!;

        public double CompositeScore { get; set; }
        public double ConsistencyScore { get; set; }
        public double EngagementQualityScore { get; set; }
        public double GrowthStabilityScore { get; set; }
        public double ContentRelevanceScore { get; set; }
        public double BrandSafetyScore { get; set; }

        public string Trend7d { get; set; } = "flat";
        public string Trend30d { get; set; } = "flat";
        public double Delta7dPercent { get; set; }
        public double Delta30dPercent { get; set; }

        public double SuspiciousEngagementRatio { get; set; }
        public double LikeCommentViewConsistencyScore { get; set; }
        public bool EngagementVolatilityFlag { get; set; }
        public double EngagementVolatilityScore { get; set; }
        public double ReusedCommentPatternScore { get; set; }

        public string WhyExplanation { get; set; } = string.Empty;
        public string AudienceExplanation { get; set; } = string.Empty;
        public string BestPostingWindow { get; set; } = string.Empty;
        public string ContentFormatPerformanceJson { get; set; } = "[]";
        public string RetentionSuggestionsJson { get; set; } = "[]";
        public string WeeklyActionsJson { get; set; } = "[]";

        public DateTime CalculatedAt { get; set; }
    }
}
