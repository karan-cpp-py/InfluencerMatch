using System.Threading.Tasks;
using InfluencerMatch.Application.DTOs;

namespace InfluencerMatch.Application.Interfaces
{
    public interface ICreatorAnalyticsService
    {
        /// <summary>Return full analytics profile for a single creator.</summary>
        Task<CreatorAnalyticsDto?> GetCreatorAnalyticsAsync(int creatorId);

        /// <summary>
        /// (Re)calculate and persist CreatorAnalytics for one creator.
        /// Called by the background worker and on-demand.
        /// </summary>
        Task<bool> RefreshAnalyticsAsync(int creatorId);

        /// <summary>Refresh analytics for all creators (batch job).</summary>
        Task RefreshAllAnalyticsAsync();

        /// <summary>Search creators with filters, returns paged results.</summary>
        Task<PagedResultDto<CreatorSearchResultDto>> SearchCreatorsAsync(CreatorSearchQueryDto query);

        /// <summary>Record current subscriber count snapshot for all creators.</summary>
        Task RecordGrowthSnapshotAsync();
    }
}
