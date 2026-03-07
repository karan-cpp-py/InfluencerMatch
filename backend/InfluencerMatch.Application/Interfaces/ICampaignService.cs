using System.Collections.Generic;
using System.Threading.Tasks;
using InfluencerMatch.Application.DTOs;

namespace InfluencerMatch.Application.Interfaces
{
    public interface ICampaignService
    {
            Task<CampaignDto> CreateAsync(CampaignDto dto);
        Task<CampaignDto> GetByIdAsync(int id);
        Task<IEnumerable<CampaignDto>> GetByBrandIdAsync(int brandId);
        Task<IEnumerable<CampaignDto>> GetAllAsync();
        Task<CampaignDto> UpdateAsync(int id, CampaignDto dto);
    }
}