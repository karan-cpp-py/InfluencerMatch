using System.Collections.Generic;
using System.Threading.Tasks;
using InfluencerMatch.Application.DTOs;
using InfluencerMatch.Domain.Entities;

namespace InfluencerMatch.Application.Interfaces
{
    public interface ICreatorRepository : IRepository<Creator>
    {
        Task<Creator?> GetByChannelIdAsync(string channelId);
        Task<List<Creator>> GetAllWithUsersAsync();
        Task<Dictionary<int, CreatorAnalytics>> GetLatestAnalyticsMapAsync(IEnumerable<int> creatorIds);
        Task<List<Creator>> GetAllWithAnalyticsAsync();
        Task<PagedResultDto<CreatorSearchResultDto>> SearchAsync(CreatorSearchQueryDto query);
        Task UpsertAnalyticsAsync(CreatorAnalytics analytics);
        Task AddGrowthSnapshotAsync(CreatorGrowth growth);
        Task<List<CreatorGrowth>> GetGrowthHistoryAsync(int creatorId, int maxPoints = 30);
        Task<CreatorAnalytics?> GetLatestAnalyticsAsync(int creatorId);
    }
}
