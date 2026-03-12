using System;

namespace InfluencerMatch.Domain.Entities
{
    public class CampaignStrategySnapshot
    {
        public int CampaignStrategySnapshotId { get; set; }
        public int CampaignId { get; set; }
        public Campaign Campaign { get; set; } = null!;

        public string NegotiationIntelligenceJson { get; set; } = "{}";
        public string CreativeBriefIntelligenceJson { get; set; } = "{}";

        public DateTime CalculatedAt { get; set; }
    }
}
