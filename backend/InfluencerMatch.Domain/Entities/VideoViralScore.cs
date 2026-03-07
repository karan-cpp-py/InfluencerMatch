using System;

namespace InfluencerMatch.Domain.Entities
{
    /// <summary>
    /// Persisted viral score for a single YouTube video.
    ///
    /// Algorithm:
    ///   ViewsVelocity      = ViewCount  / max(HoursSincePublish, 1)
    ///   GrowthAcceleration = ViewCount  / max(HoursSincePublish², 1)
    ///   EngagementMomentum = (Likes + Comments) / max(ViewCount, 1)
    ///
    ///   ViralScore = 0.5 × norm(ViewsVelocity)
    ///              + 0.3 × norm(GrowthAcceleration)
    ///              + 0.2 × norm(EngagementMomentum)
    ///
    /// Each metric is normalised 0-1 across all videos scored in the same batch.
    /// </summary>
    public class VideoViralScore
    {
        public int ScoreId { get; set; }

        public string VideoId { get; set; } = string.Empty;

        /// <summary>Composite score 0.0 – 1.0.</summary>
        public double ViralScore { get; set; }

        /// <summary>Raw views-per-hour (un-normalised).</summary>
        public double ViewsVelocityRaw { get; set; }

        /// <summary>Normalised views velocity component (0-1).</summary>
        public double ViewsVelocity { get; set; }

        /// <summary>Normalised growth-acceleration component (0-1).</summary>
        public double GrowthAcceleration { get; set; }

        /// <summary>Normalised engagement-momentum component (0-1).</summary>
        public double EngagementMomentum { get; set; }

        public DateTime CalculatedAt { get; set; }

        // Navigation
        public Video Video { get; set; } = null!;
    }
}
