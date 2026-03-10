using System.Threading.Tasks;
using InfluencerMatch.Application.DTOs;

namespace InfluencerMatch.Application.Interfaces
{
    public interface IYouTubeVideoAnalysisService
    {
        Task<object> AnalyzeLatestVideoAsync(YouTubeVideoAnalysisRequestDto request);
    }
}
