using InfluencerMatch.Domain.Entities;

namespace InfluencerMatch.Application.Interfaces
{
    public interface ITokenRepository
    {
        Task AddRefreshTokenAsync(RefreshToken token);
        Task<RefreshToken?> GetByTokenHashAsync(string tokenHash);
        Task SaveChangesAsync();
    }
}
