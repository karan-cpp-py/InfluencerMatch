using System.Threading.Tasks;
using System.Collections.Generic;
using InfluencerMatch.Domain.Entities;

namespace InfluencerMatch.Application.Interfaces
{
    public interface IInfluencerRepository : IRepository<Influencer>
    {
        Task<Influencer> GetByUserIdAsync(int userId);
        Task<List<Influencer>> GetAllWithUsersAsync();
    }
}