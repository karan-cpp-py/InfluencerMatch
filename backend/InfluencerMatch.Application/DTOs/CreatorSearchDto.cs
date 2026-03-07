using System.Collections.Generic;

namespace InfluencerMatch.Application.DTOs
{
    /// <summary>Single item returned in creator search — GET /api/creators/search</summary>
    public class CreatorSearchResultDto
    {
        public int    CreatorId    { get; set; }
        public string ChannelId    { get; set; } = string.Empty;
        public string ChannelName  { get; set; } = string.Empty;
        public string Platform     { get; set; } = string.Empty;
        public string Category     { get; set; } = string.Empty;
        public string Country      { get; set; } = string.Empty;
        public long   Subscribers  { get; set; }
        public long   TotalViews   { get; set; }
        public int    VideoCount   { get; set; }
        public double EngagementRate { get; set; }
        public double AvgViews     { get; set; }
        public double AvgLikes     { get; set; }
        // Creator segmentation
        public string? CreatorTier    { get; set; }
        public bool    IsSmallCreator { get; set; }
        // Language detection
        public string? Language              { get; set; }
        public string? Region                { get; set; }
        public double? LanguageConfidenceScore { get; set; }
        // Rising / growth
        public double? GrowthRate   { get; set; }
        public double? CreatorScore { get; set; }
    }

    public class PagedResultDto<T>
    {
        public List<T> Items      { get; set; } = new();
        public int TotalCount     { get; set; }
        public int Page           { get; set; }
        public int PageSize       { get; set; }
        public int TotalPages     { get; set; }
    }

    /// <summary>Query object for creator search filters</summary>
    public class CreatorSearchQueryDto
    {
        public string? Category    { get; set; }
        public string? Platform    { get; set; }
        public long?   MinSubscribers { get; set; }
        public long?   MaxSubscribers { get; set; }
        public double? MinEngagement  { get; set; }
        public double? MaxEngagement  { get; set; }
        public string? Search      { get; set; }
        public string? Country     { get; set; }  // e.g. "IN" to show only Indian creators
        public string? Language    { get; set; }  // e.g. "Hindi", "Tamil"
        public string? Region      { get; set; }  // e.g. "India"
        public string? CreatorTier { get; set; }  // e.g. "Micro", "MidTier"

        /// <summary>
        /// When true (default), only return creators with 1K–500K subscribers.
        /// Set to false for internal/analytics queries that need the full dataset.
        /// </summary>
        public bool OnlySmallCreators { get; set; } = true;

        public string SortBy  { get; set; } = "subscribers";
        public int    Page     { get; set; } = 1;
        public int    PageSize { get; set; } = 20;
    }
}
