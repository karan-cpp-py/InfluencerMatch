using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace InfluencerMatch.Application.Interfaces
{
    /// <summary>
    /// Detects the primary content language for each creator by analysing
    /// video titles, descriptions, and comment text fetched from the YouTube API.
    /// </summary>
    public interface ILanguageDetectionService
    {
        /// <summary>Run detection for a single creator and persist results to the Creator row.</summary>
        Task DetectAndSaveAsync(int creatorId, CancellationToken ct = default);

        /// <summary>Run detection for all creators (used by the background worker).</summary>
        Task RefreshAllAsync(CancellationToken ct = default);

        /// <summary>Return the ordered list of supported languages for the brand dropdown.</summary>
        IReadOnlyList<string> GetSupportedLanguages();
    }
}
