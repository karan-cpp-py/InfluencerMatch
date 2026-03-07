using InfluencerMatch.Application.DTOs;

namespace InfluencerMatch.Application.Interfaces
{
    public interface IDashboardService
    {
        Task<DashboardConfigDto> GetDashboardConfigAsync(int userId);
    }
}
