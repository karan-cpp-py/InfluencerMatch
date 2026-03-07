using System;

namespace InfluencerMatch.Domain.Entities
{
    /// <summary>
    /// Snapshot of a creator's subscriber growth velocity.
    ///
    /// GrowthRate = (SubscribersNow - Subscribers30DaysAgo) / Subscribers30DaysAgo
    ///
    /// GrowthCategory:
    ///   Rising   → GrowthRate >= 0.05   (≥ 5 % / month)
    ///   Stable   → GrowthRate >= 0.00
    ///   Declining → GrowthRate < 0.00
    /// </summary>
    public class CreatorGrowthScore
    {
        public int      CreatorGrowthScoreId { get; set; }
        public int      CreatorId            { get; set; }

        /// <summary>Monthly growth rate (can be negative). 0.10 = 10 %.</summary>
        public double   GrowthRate           { get; set; }

        /// <summary>Rising | Stable | Declining</summary>
        public string   GrowthCategory       { get; set; } = "Stable";

        /// <summary>Absolute subscriber delta in the last 30 days.</summary>
        public long     SubscriberDelta      { get; set; }

        /// <summary>Subscribers at the start of the 30-day window.</summary>
        public long     BaselineSubscribers  { get; set; }

        public DateTime CalculatedAt         { get; set; }

        // Navigation
        public Creator  Creator              { get; set; } = null!;
    }
}
