using System;
using System.Collections.Generic;

namespace InfluencerMatch.Application.DTOs
{
    // ── Per-video record ───────────────────────────────────────────────────────

    public class VideoAnalyticsItemDto
    {
        public string   YoutubeVideoId { get; set; } = string.Empty;
        public string   Title          { get; set; } = string.Empty;
        public long     Views          { get; set; }
        public long     Likes          { get; set; }
        public long     Comments       { get; set; }
        public double   EngagementRate { get; set; }
        public string?  BrandName      { get; set; }
        public string?  ProductName    { get; set; }
        public double   DetectionConfidence { get; set; }
        public string   VideoType      { get; set; } = "Organic";
        public DateTime PublishedAt    { get; set; }
    }

    // ── Creator summary — GET /api/creators/{id}/video-analytics ─────────────

    public class CreatorVideoAnalyticsSummaryDto
    {
        public int    CreatorId          { get; set; }
        public string ChannelName        { get; set; } = string.Empty;

        // Overall
        public int    TotalVideos        { get; set; }
        public double AvgViews           { get; set; }
        public double AvgEngagementRate  { get; set; }

        // Organic bucket
        public int    OrganicVideos      { get; set; }
        public double AvgOrganicViews    { get; set; }
        public double AvgOrganicEng      { get; set; }

        // Sponsored bucket
        public int    SponsoredVideos    { get; set; }
        public double AvgSponsoredViews  { get; set; }
        public double AvgSponsoredEng    { get; set; }

        // Brands detected for this creator
        public List<string> DetectedBrands { get; set; } = new();

        // Individual video rows (most recent first)
        public List<VideoAnalyticsItemDto> Videos { get; set; } = new();
    }

    // ── Brand-level aggregated stats — GET /api/brands/{brand}/creators ──────

    public class BrandCreatorStatsDto
    {
        public string BrandName      { get; set; } = string.Empty;
        public int    TotalCreators  { get; set; }
        public int    TotalVideos    { get; set; }
        public double TotalViews     { get; set; }
        public double AvgEngagement  { get; set; }

        public List<BrandCreatorEntryDto> Creators { get; set; } = new();
    }

    public class BrandCreatorEntryDto
    {
        public int    CreatorId      { get; set; }
        public string ChannelName    { get; set; } = string.Empty;
        public long   Subscribers    { get; set; }
        public string Category       { get; set; } = string.Empty;
        public int    VideoCount     { get; set; }
        public double TotalViews     { get; set; }
        public double AvgEngagement  { get; set; }
        public DateTime LastDetectedAt { get; set; }
    }
}
