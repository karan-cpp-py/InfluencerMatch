using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using InfluencerMatch.Application.DTOs;

namespace InfluencerMatch.Application.Interfaces
{
    /// <summary>
    /// Calculates and stores composite creator scores.
    /// </summary>
    public interface ICreatorScoringService
    {
        /// <summary>Recalculate and persist the score for a single creator.</summary>
        Task<CreatorScoreDto?> CalculateScoreAsync(int creatorId);

        /// <summary>Recalculate scores for every creator (used by the background worker).</summary>
        Task RecalculateAllScoresAsync(CancellationToken ct = default);

        /// <summary>Returns the latest stored score for a creator, or null if not yet calculated.</summary>
        Task<CreatorScoreDto?> GetScoreAsync(int creatorId);

        /// <summary>
        /// Returns the side-by-side comparison of two creators.
        /// Returns null for either side if the creator does not exist.
        /// </summary>
        Task<CreatorComparisonDto> CompareCreatorsAsync(int creatorId1, int creatorId2);

        /// <summary>
        /// Returns a paged, score-sorted list of creators — the leaderboard.
        /// </summary>
        Task<PagedResultDto<CreatorScoreDto>> GetLeaderboardAsync(
            int page, int pageSize, string? category = null, string? country = null);
    }
}
