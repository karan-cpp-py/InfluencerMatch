using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using InfluencerMatch.Application.DTOs;

namespace InfluencerMatch.Application.Interfaces
{
    /// <summary>
    /// Feature 1 — detect creators whose subscriber growth rate is accelerating.
    /// </summary>
    public interface IRisingCreatorService
    {
        /// <summary>
        /// Recompute GrowthRate + GrowthCategory for every creator that has
        /// at least two CreatorGrowth snapshots ≥ 25 days apart.
        /// </summary>
        Task RecalculateAllGrowthScoresAsync(CancellationToken ct = default);

        /// <summary>
        /// Returns the top-N creators ordered by GrowthRate descending.
        /// Optionally filter by GrowthCategory ("Rising" | "Stable" | "Declining")
        /// and/or by country.
        /// </summary>
        Task<List<RisingCreatorDto>> GetRisingCreatorsAsync(
            int topN = 50,
            string? growthCategory = null,
            string? country = null);
    }
}
