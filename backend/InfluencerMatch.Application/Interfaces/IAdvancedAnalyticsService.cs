using System.Threading;
using System.Threading.Tasks;
using InfluencerMatch.Application.DTOs;

namespace InfluencerMatch.Application.Interfaces
{
    public interface IAdvancedAnalyticsService
    {
        Task<CreatorInsightsDto?> GetCreatorInsightsAsync(
            int creatorId,
            string? brandCategory = null,
            string? brandCountry = null,
            string? brandLanguage = null,
            CancellationToken ct = default);

        Task<CreatorInsightsDto?> GetCreatorSelfInsightsAsync(
            int userId,
            CancellationToken ct = default);

        Task<CreatorBrandFitDto?> GetCreatorBrandFitAsync(
            int creatorId,
            string? brandCategory,
            string? brandCountry,
            string? brandLanguage,
            CancellationToken ct = default);

        Task<CampaignOutcomeAnalyticsDto?> GetCampaignOutcomeAnalyticsAsync(
            int campaignId,
            CancellationToken ct = default);

        Task<PreCampaignForecastDto?> GetPreCampaignForecastAsync(
            int campaignId,
            decimal? budgetOverride = null,
            CancellationToken ct = default);

        Task<OpportunityRadarDto> GetOpportunityRadarAsync(
            string? category,
            string? country,
            string? language,
            int limit = 10,
            CancellationToken ct = default);

        Task<SponsorshipReadinessDto?> GetSponsorshipReadinessAsync(
            int creatorId,
            CancellationToken ct = default);

        Task<NegotiationIntelligenceDto?> GetNegotiationIntelligenceAsync(
            int campaignId,
            decimal? proposedPrice = null,
            CancellationToken ct = default);

        Task<CreativeBriefIntelligenceDto?> GetCreativeBriefIntelligenceAsync(
            int campaignId,
            string? campaignGoal = null,
            CancellationToken ct = default);

        Task<CompetitorShareOfVoiceDto> GetCompetitorShareOfVoiceAsync(
            string brandName,
            string? category,
            string? country,
            string? language,
            CancellationToken ct = default);

        Task<RegionalLanguagePerformanceDto> GetRegionalLanguagePerformanceAsync(
            string? category,
            string? country,
            string? brandLanguage,
            CancellationToken ct = default);
    }
}
