using System;
using System.Collections.Generic;

namespace InfluencerMatch.Domain.Entities
{
    /// <summary>
    /// A YouTube video belonging to a registered creator's <see cref="CreatorChannel"/>.
    /// Distinct from the scraped <see cref="Video"/> entity used for viral-content analysis.
    /// </summary>
    public class ChannelVideo
    {
        public int    Id             { get; set; }
        /// <summary>YouTube video ID, unique (e.g. "dQw4w9WgXcQ").</summary>
        public string YoutubeVideoId { get; set; } = string.Empty;
        /// <summary>YouTube channel ID — FK → CreatorChannel.ChannelId.</summary>
        public string ChannelId      { get; set; } = string.Empty;
        public CreatorChannel Channel { get; set; } = null!;

        public string  Title        { get; set; } = string.Empty;
        public string? Description  { get; set; }   // max 500 chars
        public string? ThumbnailUrl { get; set; }
        public string? Tags         { get; set; }   // comma-separated tags
        public string? Category     { get; set; }

        public long ViewCount    { get; set; }
        public long LikeCount    { get; set; }
        public long CommentCount { get; set; }

        public DateTime PublishedAt { get; set; }
        public DateTime FetchedAt   { get; set; }

        // Navigation
        public ICollection<VideoMetrics> Metrics { get; set; } = new List<VideoMetrics>();
    }
}
