using System.Threading.Tasks;
using InfluencerMatch.Application.Interfaces;
using InfluencerMatch.Domain.Entities;
using InfluencerMatch.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace InfluencerMatch.Infrastructure.Repositories
{
    public class UserRepository : Repository<User>, IUserRepository
    {
        public UserRepository(ApplicationDbContext context) : base(context)
        {
        }

        public async Task<User> GetByEmailAsync(string email)
        {
            return await _dbSet.FirstOrDefaultAsync(u => u.Email == email);
        }

        public async Task<User?> GetByEmailVerificationTokenAsync(string token)
        {
            return await _dbSet.FirstOrDefaultAsync(u => u.EmailVerificationToken == token);
        }

        public async Task<User?> GetByPasswordResetTokenAsync(string token)
        {
            return await _dbSet.FirstOrDefaultAsync(u => u.PasswordResetToken == token);
        }
    }
}