using System;

namespace InfluencerMatch.Domain.Entities
{
    /// <summary>
    /// Per-video analytics snapshot — stores metrics and brand-collaboration
    /// classification for every scanned video.
    /// </summary>
    public class VideoAnalytics
    {
        public int      VideoAnalyticsId { get; set; }

        /// <summary>YouTube video ID (e.g. "dQw4w9WgXcQ").</summary>
        public string   YoutubeVideoId   { get; set; } = string.Empty;

        public int      CreatorId        { get; set; }

        public string   Title            { get; set; } = string.Empty;

        public long     Views            { get; set; }
        public long     Likes            { get; set; }
        public long     Comments         { get; set; }

        /// <summary>(Likes + Comments) / Views × 100. 0 when Views == 0.</summary>
        public double   EngagementRate   { get; set; }

        /// <summary>Detected brand name, or null for organic videos.</summary>
        public string?  BrandName        { get; set; }

        /// <summary>"Sponsored" or "Organic".</summary>
        public string   VideoType        { get; set; } = "Organic";

        public DateTime PublishedAt      { get; set; }
        public DateTime RecordedAt       { get; set; }

        public Creator  Creator          { get; set; } = null!;
    }
}
