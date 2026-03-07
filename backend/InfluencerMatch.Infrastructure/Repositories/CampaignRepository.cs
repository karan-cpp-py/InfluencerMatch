using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using InfluencerMatch.Application.Interfaces;
using InfluencerMatch.Domain.Entities;
using InfluencerMatch.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace InfluencerMatch.Infrastructure.Repositories
{
    public class CampaignRepository : Repository<Campaign>, ICampaignRepository
    {
        public CampaignRepository(ApplicationDbContext context) : base(context)
        {
        }

        public async Task<IEnumerable<Campaign>> GetByBrandIdAsync(int brandId)
        {
            return await _dbSet.Where(c => c.BrandId == brandId).ToListAsync();
        }
    }
}