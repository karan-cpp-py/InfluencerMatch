using System.Collections.Generic;
using System.Threading.Tasks;
using InfluencerMatch.Application.DTOs;

namespace InfluencerMatch.Application.Interfaces
{
    public interface IInfluencerService
    {
        Task<InfluencerDto> CreateAsync(InfluencerDto dto);
        Task<InfluencerDto> UpdateAsync(int id, InfluencerDto dto);
        Task<InfluencerDto> GetByIdAsync(int id);
        Task<IEnumerable<InfluencerDto>> GetAllAsync();
    }
}