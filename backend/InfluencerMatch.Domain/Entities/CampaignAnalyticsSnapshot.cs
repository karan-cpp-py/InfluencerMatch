using System;

namespace InfluencerMatch.Domain.Entities
{
    public class CampaignAnalyticsSnapshot
    {
        public int CampaignAnalyticsSnapshotId { get; set; }
        public int CampaignId { get; set; }
        public Campaign Campaign { get; set; } = null!;

        public long Reach { get; set; }
        public double EngagedViews { get; set; }
        public double EngagementRate { get; set; }
        public double Cpm { get; set; }
        public double Cpe { get; set; }
        public double CpcLikeProxy { get; set; }
        public int OverperformerCount { get; set; }
        public int UnderperformerCount { get; set; }
        public string CreatorContributionsJson { get; set; } = "[]";

        public double EstimatedViewsLow { get; set; }
        public double EstimatedViewsExpected { get; set; }
        public double EstimatedViewsHigh { get; set; }
        public double ExpectedEngagementLow { get; set; }
        public double ExpectedEngagementExpected { get; set; }
        public double ExpectedEngagementHigh { get; set; }
        public double ConfidenceScore { get; set; }
        public string ConfidenceTier { get; set; } = "Low";
        public string BudgetScenariosJson { get; set; } = "[]";

        public DateTime CalculatedAt { get; set; }
    }
}
