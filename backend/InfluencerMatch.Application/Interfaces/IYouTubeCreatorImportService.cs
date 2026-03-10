using System.Threading;
using System.Threading.Tasks;
using InfluencerMatch.Application.DTOs;

namespace InfluencerMatch.Application.Interfaces
{
    /// <summary>
    /// Searches YouTube for channels matching a query, parses contact hints from
    /// channel descriptions, and upserts results into the Creators table.
    /// Triggered manually by SuperAdmin only — never runs automatically.
    /// </summary>
    public interface IYouTubeCreatorImportService
    {
        Task<YouTubeImportResultDto> ImportAsync(
            YouTubeImportRequestDto request,
            CancellationToken ct = default);
    }
}
