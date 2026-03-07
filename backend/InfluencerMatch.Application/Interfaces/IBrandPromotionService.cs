using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using InfluencerMatch.Application.DTOs;

namespace InfluencerMatch.Application.Interfaces
{
    /// <summary>
    /// Scans YouTube video metadata for brand promotion signals (hashtags, @-mentions, keywords).
    /// </summary>
    public interface IBrandPromotionService
    {
        /// <summary>
        /// Scan the latest videos of a single creator and persist any detected brand mentions.
        /// Returns the number of new mentions stored.
        /// </summary>
        Task<int> ScanCreatorAsync(int creatorId, CancellationToken ct = default);

        /// <summary>Scan all creators in the database (used by the background worker).</summary>
        Task ScanAllCreatorsAsync(CancellationToken ct = default);

        /// <summary>
        /// Returns all known brand mentions for a given brand name (case-insensitive, partial match).
        /// </summary>
        Task<List<BrandMentionDto>> GetMentionsForBrandAsync(string brandName);

        /// <summary>Returns aggregated campaign analytics for a brand.</summary>
        Task<BrandAnalyticsDto?> GetBrandAnalyticsAsync(string brandName);
    }
}
