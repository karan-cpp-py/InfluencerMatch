using InfluencerMatch.Application.DTOs;

namespace InfluencerMatch.Application.Interfaces
{
    public interface ISubscriptionAccessService
    {
        Task<SearchAccessResultDto> ValidateCreatorSearchAccessAsync(int userId);
        Task<FeatureAccessResultDto> ValidateFeatureAccessAsync(int userId, string featureKey);
        Task RecordCreatorSearchUsageAsync(int userId);
    }
}
