using System;

namespace InfluencerMatch.Domain.Entities
{
    public class CreatorReadinessSnapshot
    {
        public int CreatorReadinessSnapshotId { get; set; }
        public int CreatorId { get; set; }
        public Creator Creator { get; set; } = null!;

        public double SponsorshipReadinessIndex { get; set; }
        public double ReliabilityScore { get; set; }
        public double ContentHygieneScore { get; set; }
        public double BrandFitStabilityScore { get; set; }
        public double ConversionProxyScore { get; set; }

        public string ReadinessRoadmapJson { get; set; } = "[]";
        public DateTime CalculatedAt { get; set; }
    }
}
