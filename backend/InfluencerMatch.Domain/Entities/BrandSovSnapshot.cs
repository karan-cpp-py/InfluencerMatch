using System;

namespace InfluencerMatch.Domain.Entities
{
    public class BrandSovSnapshot
    {
        public int BrandSovSnapshotId { get; set; }
        public string BrandName { get; set; } = string.Empty;
        public string Category { get; set; } = "General";
        public string Country { get; set; } = "GLOBAL";
        public string Language { get; set; } = "Any";

        public string CompetitorSovJson { get; set; } = "{}";
        public string RegionalLanguagePerformanceJson { get; set; } = "{}";

        public DateTime CalculatedAt { get; set; }
    }
}
