using System;
using System.Collections.Generic;

namespace InfluencerMatch.Application.DTOs
{
    // ── Individual mention record ──────────────────────────────────────────────

    public class BrandMentionDto
    {
        public int    BrandMentionId  { get; set; }
        public string VideoId         { get; set; } = string.Empty;
        public string VideoTitle      { get; set; } = string.Empty;
        public int    CreatorId       { get; set; }
        public string ChannelName     { get; set; } = string.Empty;
        public string BrandName       { get; set; } = string.Empty;
        public string DetectionMethod { get; set; } = string.Empty;
        public double ConfidenceScore { get; set; }
        public DateTime DetectedAt    { get; set; }
    }

    // ── Per-brand aggregated analytics (Feature 4) ────────────────────────────

    public class BrandAnalyticsDto
    {
        public string BrandName                           { get; set; } = string.Empty;

        /// <summary>Distinct creators who have promoted this brand.</summary>
        public int    TotalCreators                       { get; set; }

        /// <summary>Total videos where the brand was detected.</summary>
        public int    TotalVideos                         { get; set; }

        /// <summary>Sum of AvgViews × mention-video weight across all promoting creators.</summary>
        public double EstimatedTotalViews                 { get; set; }

        /// <summary>Sum of AvgViews × EngagementRate × mention-video weight.</summary>
        public double EstimatedTotalEngagement            { get; set; }

        /// <summary>Average confidence score of all detections.</summary>
        public double AverageConfidenceScore              { get; set; }

        public List<BrandPromotingCreatorDto> Creators    { get; set; } = new();
    }

    public class BrandPromotingCreatorDto
    {
        public int    CreatorId       { get; set; }
        public string ChannelName     { get; set; } = string.Empty;
        public string Platform        { get; set; } = string.Empty;
        public string Category        { get; set; } = string.Empty;
        public long   Subscribers     { get; set; }
        public double EngagementRate  { get; set; }
        public int    MentionCount    { get; set; }

        /// <summary>Estimated views generated for this brand by this creator.</summary>
        public double EstimatedViews  { get; set; }
    }
}
