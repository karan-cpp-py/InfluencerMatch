using System;
using System.Collections.Generic;

namespace InfluencerMatch.Application.DTOs
{
    // ── Creator Registration ─────────────────────────────────────────────────

    public class CreatorRegisterRequestDto
    {
        public string  Name            { get; set; } = string.Empty;
        public string  Email           { get; set; } = string.Empty;
        public string  Password        { get; set; } = string.Empty;
        public string? Country         { get; set; }
        public string? Language        { get; set; }
        public string? Category        { get; set; }
        public string? InstagramHandle { get; set; }
        public string? ContactEmail    { get; set; }
        public string? Bio             { get; set; }
    }

    public class CreatorRegisterResponseDto
    {
        public string Token            { get; set; } = string.Empty;
        public int    CreatorProfileId { get; set; }
        public int    UserId           { get; set; }
    }

    public class UpdateCreatorProfileDto
    {
        public string? Country         { get; set; }
        public string? Language        { get; set; }
        public string? Category        { get; set; }
        public string? InstagramHandle { get; set; }
        public string? ContactEmail    { get; set; }
        public string? Bio             { get; set; }
    }

    public class CreatorProfileDto
    {
        public int     CreatorProfileId { get; set; }
        public int     UserId           { get; set; }
        public string  Name             { get; set; } = string.Empty;
        public string  Email            { get; set; } = string.Empty;
        public string? Country          { get; set; }
        public string? Language         { get; set; }
        public string? Category         { get; set; }
        public string? InstagramHandle  { get; set; }
        public string? ContactEmail     { get; set; }
        public string? Bio              { get; set; }
        public DateTime CreatedAt       { get; set; }
    }

    // ── Channel Linking ──────────────────────────────────────────────────────

    public class LinkChannelRequestDto
    {
        /// <summary>
        /// Supported formats:
        ///   https://youtube.com/channel/UCxxx
        ///   https://youtube.com/@handle
        ///   https://youtube.com/c/name
        /// </summary>
        public string ChannelUrl { get; set; } = string.Empty;
    }

    public class CreatorChannelDto
    {
        public int     Id               { get; set; }
        public string  ChannelId        { get; set; } = string.Empty;
        public int     CreatorProfileId { get; set; }
        public string  ChannelName      { get; set; } = string.Empty;
        public string? Description      { get; set; }
        public string? ChannelUrl       { get; set; }
        public string? ThumbnailUrl     { get; set; }
        public long    Subscribers      { get; set; }
        public long    TotalViews       { get; set; }
        public int     VideoCount       { get; set; }
        public double  EngagementRate   { get; set; }
        public string? CreatorTier      { get; set; }
        public bool    IsVerified       { get; set; }
        public DateTime? ChannelPublishedAt  { get; set; }
        public DateTime? LastStatsUpdatedAt  { get; set; }
        public DateTime  CreatedAt           { get; set; }
    }

    public class ChannelVideoDto
    {
        public string   YoutubeVideoId { get; set; } = string.Empty;
        public string   ChannelId      { get; set; } = string.Empty;
        public string   Title          { get; set; } = string.Empty;
        public string?  ThumbnailUrl   { get; set; }
        public string?  Tags           { get; set; }
        public string?  Category       { get; set; }
        public long     ViewCount      { get; set; }
        public long     LikeCount      { get; set; }
        public long     CommentCount   { get; set; }
        public DateTime PublishedAt    { get; set; }
    }

    // ── Creator Dashboard ────────────────────────────────────────────────────

    public class CreatorDashboardDto
    {
        public CreatorProfileDto      Profile              { get; set; } = new();
        public CreatorChannelDto?     Channel              { get; set; }
        public List<ChannelVideoDto>  RecentVideos         { get; set; } = new();
        public int                    PendingCollaborations { get; set; }
        public double                 AvgViewsPerVideo     { get; set; }
    }

    public class CreatorOnboardingStatusDto
    {
        public int ProfileCompletenessPercent { get; set; }
        public bool ChannelLinked { get; set; }
        public int WeeklyAlertCount { get; set; }
        public List<CreatorOnboardingStepDto> Steps { get; set; } = new();
        public List<string> ScoreChangeExplanations { get; set; } = new();
        public List<string> WeeklyInsights { get; set; } = new();
    }

    public class CreatorOnboardingStepDto
    {
        public string Key { get; set; } = string.Empty;
        public string Title { get; set; } = string.Empty;
        public bool Completed { get; set; }
    }

    // ── Marketplace (Brand Discovery) ────────────────────────────────────────

    public class MarketplaceSearchQueryDto
    {
        public string? Search          { get; set; }
        public string? Language        { get; set; }
        public string? Category        { get; set; }
        public string? Country         { get; set; }
        public string? Region          { get; set; }
        public string? CreatorTier     { get; set; }
        public long?   MinSubscribers  { get; set; }
        public long?   MaxSubscribers  { get; set; }
        public double? MinEngagement   { get; set; }
        public string  SortBy          { get; set; } = "subscribers";
        public int     Page            { get; set; } = 1;
        public int     PageSize        { get; set; } = 20;
    }

    public class MarketplaceCreatorDto
    {
        public int     CreatorProfileId { get; set; }
        public string  ChannelId        { get; set; } = string.Empty;
        public string  ChannelName      { get; set; } = string.Empty;
        public string? ThumbnailUrl     { get; set; }
        public long    Subscribers      { get; set; }
        public long    TotalViews       { get; set; }
        public double  EngagementRate   { get; set; }
        public string? CreatorTier      { get; set; }
        public string? Language         { get; set; }
        public string? Category         { get; set; }
        public string? Country          { get; set; }
        public bool    IsVerified       { get; set; }
        public string? ContactEmail     { get; set; }
    }

    public class MarketplaceCreatorDetailDto : MarketplaceCreatorDto
    {
        /// <summary>Legacy Creator table ID — present only when the creator
        /// was discovered by the SuperAdmin job and exists in the Creator table.
        /// Use this to link to /creator/{id}/analytics and /creator/{id}/video-analytics.
        /// </summary>
        public int?                  CreatorId    { get; set; }
        public string?               Description  { get; set; }
        public string?               InstagramHandle { get; set; }
        public string?               Bio          { get; set; }
        public double?               GrowthRate   { get; set; }
        public string?               GrowthCategory { get; set; }
        public long?                 PredictedSubscribers12Months { get; set; }
        public int                   SponsoredVideoCount { get; set; }
        public decimal?              EstimatedSponsorshipValueInrMin { get; set; }
        public decimal?              EstimatedSponsorshipValueInrMax { get; set; }
        public AudienceDemographicsDto? AudienceDemographics { get; set; }
        public List<MarketplaceBrandCollaborationDto> BrandCollaborations { get; set; } = new();
        public List<ChannelVideoDto> RecentVideos { get; set; } = new();
    }

    public class MarketplaceBrandCollaborationDto
    {
        public string BrandName { get; set; } = string.Empty;
        public int MentionCount { get; set; }
        public DateTime? LastDetectedAt { get; set; }
        public string? SampleVideoTitle { get; set; }
    }

    // ── Collaboration Requests ───────────────────────────────────────────────

    public class SendCollaborationDto
    {
        public int     CreatorProfileId { get; set; }
        public string  CampaignTitle    { get; set; } = string.Empty;
        public decimal Budget           { get; set; }
        public string? Message          { get; set; }
    }

    public class CollaborationRequestDto
    {
        public int     RequestId        { get; set; }
        public int     BrandUserId      { get; set; }
        public string  BrandName        { get; set; } = string.Empty;
        public int     CreatorProfileId { get; set; }
        public string  ChannelName      { get; set; } = string.Empty;
        public string  CampaignTitle    { get; set; } = string.Empty;
        public decimal Budget           { get; set; }
        public string? Message          { get; set; }
        public string  Status           { get; set; } = string.Empty;
        public DateTime  CreatedAt      { get; set; }
        public DateTime? UpdatedAt      { get; set; }
    }
}
