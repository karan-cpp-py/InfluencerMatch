using System.Threading.Tasks;
using InfluencerMatch.Domain.Entities;

namespace InfluencerMatch.Application.Interfaces
{
    public interface IUserRepository : IRepository<User>
    {
        Task<User> GetByEmailAsync(string email);
        Task<User?> GetByEmailVerificationTokenAsync(string token);
        Task<User?> GetByPasswordResetTokenAsync(string token);
    }
}