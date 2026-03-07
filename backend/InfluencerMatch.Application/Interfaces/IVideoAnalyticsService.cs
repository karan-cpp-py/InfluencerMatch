using System.Threading;
using System.Threading.Tasks;
using InfluencerMatch.Application.DTOs;

namespace InfluencerMatch.Application.Interfaces
{
    /// <summary>
    /// Fetches YouTube video metrics, detects brand collaborations,
    /// and stores per-video analytics in the <c>VideoAnalytics</c> table.
    /// </summary>
    public interface IVideoAnalyticsService
    {
        /// <summary>
        /// Scan the 50 most-recent videos for a single creator, compute
        /// engagement rates, detect brand collaborations, and upsert
        /// the results into <c>VideoAnalytics</c>.
        /// Returns the number of rows upserted.
        /// </summary>
        Task<int> RefreshCreatorAsync(int creatorId, CancellationToken ct = default);

        /// <summary>
        /// Run <see cref="RefreshCreatorAsync"/> for every creator in the DB.
        /// Stops early if the YouTube API returns 403 / 429 / 401.
        /// </summary>
        Task RefreshAllAsync(CancellationToken ct = default);

        /// <summary>
        /// Returns the aggregated organic vs sponsored summary for a creator,
        /// or <c>null</c> if the creator does not exist / has no analytics yet.
        /// </summary>
        Task<CreatorVideoAnalyticsSummaryDto?> GetCreatorSummaryAsync(int creatorId);

        /// <summary>
        /// Returns all creators who promoted <paramref name="brandName"/>,
        /// with per-creator video counts, total views and avg engagement.
        /// </summary>
        Task<BrandCreatorStatsDto> GetBrandCreatorsAsync(string brandName);
    }
}
