using System;

namespace InfluencerMatch.Domain.Entities
{
    /// <summary>
    /// Composite ranking score for a creator.
    ///
    /// Formula (all components normalised to 0 – 100 before weighting):
    ///   CreatorScore = 0.4 × EngagementComponent
    ///                + 0.3 × ViewsComponent
    ///                + 0.2 × GrowthComponent
    ///                + 0.1 × FrequencyComponent
    ///
    /// Final score is in the range 0 – 100.
    /// </summary>
    public class CreatorScore
    {
        public int    ScoreId                { get; set; }
        public int    CreatorId              { get; set; }

        /// <summary>Composite score 0 – 100.</summary>
        public double Score                  { get; set; }

        // ── Component breakdown (each 0 – 100) ────────────────────
        public double EngagementComponent    { get; set; }
        public double ViewsComponent         { get; set; }
        public double GrowthComponent        { get; set; }
        public double FrequencyComponent     { get; set; }

        // ── Raw inputs stored for audit ────────────────────────────
        public double EngagementRate         { get; set; }
        public double AverageViews           { get; set; }
        public double SubscriberGrowthRate   { get; set; }
        public double UploadFrequency        { get; set; }

        public DateTime CalculatedAt         { get; set; }

        // Navigation
        public Creator Creator               { get; set; } = null!;
    }
}
