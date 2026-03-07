using System;

namespace InfluencerMatch.Domain.Entities
{
    /// <summary>
    /// Represents a YouTube video fetched for viral-content analysis.
    /// One row per video; refreshed on each background scan.
    /// </summary>
    public class Video
    {
        public int Id { get; set; }

        /// <summary>YouTube video ID (e.g. "dQw4w9WgXcQ")</summary>
        public string VideoId { get; set; } = string.Empty;

        public int CreatorId { get; set; }

        public string Title { get; set; } = string.Empty;

        public string? ThumbnailUrl { get; set; }

        /// <summary>Total view count at the time of the last fetch.</summary>
        public long ViewCount { get; set; }

        public long LikeCount { get; set; }

        public long CommentCount { get; set; }

        /// <summary>When the video was published on YouTube.</summary>
        public DateTime PublishedAt { get; set; }

        /// <summary>When this row was last updated from the YouTube API.</summary>
        public DateTime FetchedAt { get; set; }

        // Navigation
        public Creator Creator { get; set; } = null!;
    }
}
