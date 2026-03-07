using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using InfluencerMatch.Application.DTOs;

namespace InfluencerMatch.Application.Interfaces
{
    public interface IViralContentService
    {
        /// <summary>
        /// Fetches recent videos for top creators, computes viral scores and
        /// persists them to the VideoViralScores table.
        /// Called by the background worker every 2 hours.
        /// </summary>
        Task RefreshViralScoresAsync(CancellationToken ct = default);

        /// <summary>
        /// Returns the top <paramref name="topN"/> videos ordered by ViralScore descending.
        /// Optionally filtered by category or country.
        /// </summary>
        Task<List<TrendingVideoDto>> GetTrendingVideosAsync(
            int topN = 50,
            string? category = null,
            string? country = null);
    }
}
