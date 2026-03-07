using System;

namespace InfluencerMatch.Application.DTOs
{
    // ── Single creator score (Feature 2) ──────────────────────────────────────

    public class CreatorScoreDto
    {
        public int    CreatorId            { get; set; }
        public string ChannelName          { get; set; } = string.Empty;
        public string Platform             { get; set; } = string.Empty;
        public string Category             { get; set; } = string.Empty;
        public string Country              { get; set; } = string.Empty;
        public long   Subscribers          { get; set; }

        /// <summary>Composite score 0 – 100.</summary>
        public double Score                { get; set; }

        // ── Weighted components (each already multiplied by weight) ──────────
        public double EngagementComponent  { get; set; }
        public double ViewsComponent       { get; set; }
        public double GrowthComponent      { get; set; }
        public double FrequencyComponent   { get; set; }

        // ── Raw inputs ───────────────────────────────────────────────────────
        public double EngagementRate       { get; set; }
        public double AverageViews         { get; set; }

        /// <summary>Monthly subscriber growth rate (0.0 – 1.0).</summary>
        public double SubscriberGrowthRate { get; set; }

        /// <summary>Videos uploaded per week.</summary>
        public double UploadFrequency      { get; set; }

        public DateTime CalculatedAt       { get; set; }
    }

    // ── Side-by-side comparison (Feature 3) ───────────────────────────────────

    public class CreatorComparisonDto
    {
        public CreatorComparisonSideDto Creator1 { get; set; } = new();
        public CreatorComparisonSideDto Creator2 { get; set; } = new();
    }

    public class CreatorComparisonSideDto
    {
        public int    CreatorId            { get; set; }
        public string ChannelName          { get; set; } = string.Empty;
        public string Platform             { get; set; } = string.Empty;
        public string Category             { get; set; } = string.Empty;
        public long   Subscribers          { get; set; }
        public double AverageViews         { get; set; }
        public double EngagementRate       { get; set; }

        /// <summary>Videos uploaded per week.</summary>
        public double UploadFrequency      { get; set; }

        /// <summary>Composite score 0 – 100. Null if not yet calculated.</summary>
        public double? CreatorScore        { get; set; }

        public string? ScoreBreakdown      { get; set; }
    }
}
