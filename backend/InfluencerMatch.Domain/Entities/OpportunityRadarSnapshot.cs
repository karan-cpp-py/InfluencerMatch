using System;

namespace InfluencerMatch.Domain.Entities
{
    public class OpportunityRadarSnapshot
    {
        public int OpportunityRadarSnapshotId { get; set; }
        public string Category { get; set; } = "General";
        public string Country { get; set; } = "GLOBAL";
        public string Language { get; set; } = "Any";

        public string RisingCreatorsJson { get; set; } = "[]";
        public string CategoryTrendsJson { get; set; } = "[]";

        public DateTime CalculatedAt { get; set; }
    }
}
