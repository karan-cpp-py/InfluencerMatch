using System.Threading;
using System.Threading.Tasks;
using InfluencerMatch.Application.DTOs;

namespace InfluencerMatch.Application.Interfaces
{
    public interface IYouTubeSearchIntelligenceService
    {
        Task<YouTubeSearchResultDto> SearchAsync(YouTubeSearchQueryRequestDto request, CancellationToken ct = default);
        Task<YouTubeCreatorAnalysisResponseDto?> AnalyzeCreatorAsync(YouTubeCreatorAnalysisRequestDto request, CancellationToken ct = default);
        Task<YouTubeShortlistSaveResponseDto> SaveShortlistAsync(int userId, YouTubeShortlistSaveRequestDto request, CancellationToken ct = default);
    }
}
