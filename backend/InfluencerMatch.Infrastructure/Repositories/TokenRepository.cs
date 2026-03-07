using InfluencerMatch.Application.Interfaces;
using InfluencerMatch.Domain.Entities;
using InfluencerMatch.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace InfluencerMatch.Infrastructure.Repositories
{
    public class TokenRepository : ITokenRepository
    {
        private readonly ApplicationDbContext _db;

        public TokenRepository(ApplicationDbContext db)
        {
            _db = db;
        }

        public async Task AddRefreshTokenAsync(RefreshToken token)
        {
            await _db.RefreshTokens.AddAsync(token);
        }

        public Task<RefreshToken?> GetByTokenHashAsync(string tokenHash)
        {
            return _db.RefreshTokens
                .Include(t => t.User)
                .FirstOrDefaultAsync(t => t.TokenHash == tokenHash);
        }

        public Task SaveChangesAsync() => _db.SaveChangesAsync();
    }
}
