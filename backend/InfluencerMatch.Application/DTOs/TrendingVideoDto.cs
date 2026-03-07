using System;

namespace InfluencerMatch.Application.DTOs
{
    /// <summary>
    /// Returned by GET /api/videos/trending — one item per scored video.
    /// </summary>
    public class TrendingVideoDto
    {
        // ── Video identity ──────────────────────────────────────────────────
        public string VideoId { get; set; } = string.Empty;
        public string Title { get; set; } = string.Empty;
        public string? ThumbnailUrl { get; set; }
        public DateTime PublishedAt { get; set; }

        /// <summary>Hours since the video was published (at calculation time).</summary>
        public double HoursSincePublish { get; set; }

        // ── Creator info ────────────────────────────────────────────────────
        public int CreatorId { get; set; }
        public string ChannelName { get; set; } = string.Empty;
        public string Platform { get; set; } = string.Empty;
        public string Category { get; set; } = string.Empty;
        public string Country { get; set; } = string.Empty;
        public long Subscribers { get; set; }

        // ── Raw video stats ─────────────────────────────────────────────────
        public long ViewCount { get; set; }
        public long LikeCount { get; set; }
        public long CommentCount { get; set; }

        // ── Viral score components (all 0–1) ────────────────────────────────
        /// <summary>0-1: how fast views are accumulating per hour (normalised).</summary>
        public double ViewsVelocity { get; set; }

        /// <summary>0-1: early-burst momentum — rewards videos gaining speed quickly.</summary>
        public double GrowthAcceleration { get; set; }

        /// <summary>0-1: (Likes + Comments) / Views, normalised.</summary>
        public double EngagementMomentum { get; set; }

        /// <summary>Composite viral score: 0.5×Velocity + 0.3×Acceleration + 0.2×Engagement.</summary>
        public double ViralScore { get; set; }

        public DateTime CalculatedAt { get; set; }
    }
}
