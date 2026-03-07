using System.Collections.Generic;
using System.Threading.Tasks;
using InfluencerMatch.Application.DTOs;

namespace InfluencerMatch.Application.Interfaces
{
    /// <summary>Feature 2 — recommend creators for a brand category.</summary>
    public interface IBrandOpportunityService
    {
        /// <summary>
        /// Returns the top-N creators that best match a brand's target category,
        /// ranked by a composite OpportunityScore = 0.5×Engagement + 0.3×Growth + 0.2×Subscribers.
        /// </summary>
        Task<List<BrandOpportunityDto>> FindOpportunitiesAsync(BrandOpportunityRequestDto request);
    }

    /// <summary>Feature 3 — predict campaign performance metrics for a creator.</summary>
    public interface ICampaignPredictionService
    {
        Task<CampaignPredictionDto?> PredictAsync(int creatorId);
    }

    /// <summary>Feature 4 — estimate how much a brand should pay a creator.</summary>
    public interface ICreatorPricingService
    {
        Task<CreatorPriceDto?> EstimatePriceAsync(int creatorId);
    }
}
