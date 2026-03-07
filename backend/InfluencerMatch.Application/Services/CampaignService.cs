using System.Collections.Generic;
using System.Threading.Tasks;
using AutoMapper;
using InfluencerMatch.Application.DTOs;
using InfluencerMatch.Application.Interfaces;
using InfluencerMatch.Domain.Entities;

namespace InfluencerMatch.Application.Services
{
    public class CampaignService : ICampaignService
    {
        private readonly ICampaignRepository _repo;
        private readonly IMapper _mapper;

        public CampaignService(ICampaignRepository repo, IMapper mapper)
        {
            _repo = repo;
            _mapper = mapper;
        }

        public async Task<CampaignDto> CreateAsync(CampaignDto dto)
        {
            var entity = _mapper.Map<Campaign>(dto);
            var added = await _repo.AddAsync(entity);
            return _mapper.Map<CampaignDto>(added);
        }

        public async Task<IEnumerable<CampaignDto>> GetByBrandIdAsync(int brandId)
        {
            var list = await _repo.GetByBrandIdAsync(brandId);
            return _mapper.Map<IEnumerable<CampaignDto>>(list);
        }

        public async Task<CampaignDto> GetByIdAsync(int id)
        {
            var campaign = await _repo.GetByIdAsync(id);
            return _mapper.Map<CampaignDto>(campaign);
        }

        public async Task<IEnumerable<CampaignDto>> GetAllAsync()
        {
            var list = await _repo.GetAllAsync();
            return _mapper.Map<IEnumerable<CampaignDto>>(list);
        }

        public async Task<CampaignDto> UpdateAsync(int id, CampaignDto dto)
        {
            var existing = await _repo.GetByIdAsync(id);
            if (existing == null) return null;
            _mapper.Map(dto, existing);
            await _repo.UpdateAsync(existing);
            return _mapper.Map<CampaignDto>(existing);
        }
    }
}