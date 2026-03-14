using System;
using System.Collections.Generic;

namespace InfluencerMatch.Application.DTOs
{
    public class YouTubeSearchQueryRequestDto
    {
        public string Query { get; set; } = string.Empty;
        public string? Category { get; set; }
        public string? Country { get; set; }
        public string? Language { get; set; }
        public int Limit { get; set; } = 20;
    }

    public class YouTubeSearchResultDto
    {
        public string Query { get; set; } = string.Empty;
        public string IntentLabel { get; set; } = "General discovery";
        public string IntentReason { get; set; } = "Keyword pattern analysis";
        public string? AiSearchBrief { get; set; }
        public List<YouTubeSearchCreatorDto> Results { get; set; } = new();
    }

    public class YouTubeSearchCreatorDto
    {
        public int CreatorId { get; set; }
        public string ChannelId { get; set; } = string.Empty;
        public string ChannelName { get; set; } = string.Empty;
        public string? ThumbnailUrl { get; set; }
        public string Category { get; set; } = string.Empty;
        public string? Country { get; set; }
        public string? Language { get; set; }
        public long Subscribers { get; set; }
        public long TotalViews { get; set; }
        public int VideoCount { get; set; }
        public double EngagementRate { get; set; }
        public double RelevanceScore { get; set; }
        public string RelevanceReason { get; set; } = "Matched on channel metadata";
        public List<string> SuggestedActions { get; set; } = new();
        public string? ChannelUrl { get; set; }
    }

    public class YouTubeCreatorAnalysisRequestDto
    {
        public int CreatorId { get; set; }
        public string? ChannelId { get; set; }
        public string Mode { get; set; } = "last10";
        public string? SearchContext { get; set; }
    }

    public class YouTubeCreatorAnalysisResponseDto
    {
        public int CreatorId { get; set; }
        public string ChannelName { get; set; } = string.Empty;
        public string Mode { get; set; } = "last10";
        public int VideosAnalyzed { get; set; }
        public DateTime? FromDateUtc { get; set; }
        public DateTime? ToDateUtc { get; set; }
        public double AverageViews { get; set; }
        public double AverageLikes { get; set; }
        public double AverageComments { get; set; }
        public double AverageEngagementRate { get; set; }
        public double MomentumScore { get; set; }
        public string CampaignFitLabel { get; set; } = "General awareness";
        public string DataQuality { get; set; } = "Estimated";
        public string? AiNarrative { get; set; }
        public List<string> TopKeywords { get; set; } = new();
        public List<YouTubeAnalysisVideoDto> TopVideos { get; set; } = new();
        public List<string> ActionPlan { get; set; } = new();
    }

    public class YouTubeAnalysisVideoDto
    {
        public string VideoId { get; set; } = string.Empty;
        public string Title { get; set; } = string.Empty;
        public long ViewCount { get; set; }
        public long LikeCount { get; set; }
        public long CommentCount { get; set; }
        public double EngagementRate { get; set; }
        public DateTime PublishedAt { get; set; }
    }

    public class YouTubeShortlistSaveRequestDto
    {
        public List<int> CreatorIds { get; set; } = new();
        public List<string> ChannelIds { get; set; } = new();
        public int? CampaignId { get; set; }
        public string? SearchQuery { get; set; }
        public string? Title { get; set; }
        public string? Notes { get; set; }
    }

    public class YouTubeShortlistSaveResponseDto
    {
        public bool Saved { get; set; }
        public int SavedCount { get; set; }
        public bool LoggedToWorkspace { get; set; }
        public string Message { get; set; } = string.Empty;
        public string? WorkspaceRoute { get; set; }
        public string? CampaignRoute { get; set; }
        public string? CreatorIntelligenceRoute { get; set; }
        public List<int> CreatorIds { get; set; } = new();
        public List<string> ChannelIds { get; set; } = new();
    }
}
