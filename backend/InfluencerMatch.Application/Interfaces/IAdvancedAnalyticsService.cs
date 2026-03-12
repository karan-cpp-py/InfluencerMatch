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
    }
}
