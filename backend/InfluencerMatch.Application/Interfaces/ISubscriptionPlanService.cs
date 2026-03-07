using InfluencerMatch.Application.DTOs;

namespace InfluencerMatch.Application.Interfaces
{
    public interface ISubscriptionPlanService
    {
        Task<List<SubscriptionPlanDto>> GetPlansAsync();
        Task SeedDefaultPlansAsync();
    }
}
