using System;
using System.Collections.Generic;

namespace InfluencerMatch.Application.DTOs
{
    /// <summary>Full analytics profile for a single creator — GET /api/creators/{creatorId}/analytics</summary>
    public class CreatorAnalyticsDto
    {
        public int CreatorId { get; set; }
        public string ChannelName { get; set; } = string.Empty;
        public string ChannelId { get; set; } = string.Empty;
        public string Platform { get; set; } = string.Empty;
        public string Category { get; set; } = string.Empty;
        public long Subscribers { get; set; }
        public int TotalVideos { get; set; }

        // Computed analytics
        public double AverageViews { get; set; }
        public double AverageLikes { get; set; }
        public double AverageComments { get; set; }
        public double EngagementRate { get; set; }
        public DateTime? AnalyticsCalculatedAt { get; set; }

        // Growth history (most recent first)
        public List<GrowthPointDto> GrowthHistory { get; set; } = new();

        // Top videos placeholder (YouTube API would populate this in a real integration)
        public List<TopVideoDto> TopVideos { get; set; } = new();
    }

    public class GrowthPointDto
    {
        public DateTime RecordedAt { get; set; }
        public long Subscribers { get; set; }
        public long? Delta { get; set; }   // change vs previous snapshot
    }

    public class TopVideoDto
    {
        public string VideoId { get; set; } = string.Empty;
        public string Title { get; set; } = string.Empty;
        public long Views { get; set; }
        public long Likes { get; set; }
        public string ThumbnailUrl { get; set; } = string.Empty;
    }
}
