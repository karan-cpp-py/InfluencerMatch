using System.Threading.Tasks;
using InfluencerMatch.Application.Interfaces;
using InfluencerMatch.Domain.Entities;
using InfluencerMatch.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace InfluencerMatch.Infrastructure.Repositories
{
    public class InfluencerRepository : Repository<Influencer>, IInfluencerRepository
    {
        public InfluencerRepository(ApplicationDbContext context) : base(context)
        {
        }

        public async Task<Influencer> GetByUserIdAsync(int userId)
        {
            return await _dbSet.Include(i => i.User).FirstOrDefaultAsync(i => i.UserId == userId);
        }

        public async Task<List<Influencer>> GetAllWithUsersAsync()
        {
            return await _dbSet
                .AsNoTracking()
                .Include(i => i.User)
                .ToListAsync();
        }
    }
}