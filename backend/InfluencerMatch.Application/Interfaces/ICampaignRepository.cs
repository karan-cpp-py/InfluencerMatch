using System.Collections.Generic;
using System.Threading.Tasks;
using InfluencerMatch.Domain.Entities;

namespace InfluencerMatch.Application.Interfaces
{
    public interface ICampaignRepository : IRepository<Campaign>
    {
        Task<IEnumerable<Campaign>> GetByBrandIdAsync(int brandId);
    }
}