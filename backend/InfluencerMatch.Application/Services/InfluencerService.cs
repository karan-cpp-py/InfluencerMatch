using System.Collections.Generic;
using System.Threading.Tasks;
using AutoMapper;
using InfluencerMatch.Application.DTOs;
using InfluencerMatch.Application.Interfaces;
using InfluencerMatch.Domain.Entities;

namespace InfluencerMatch.Application.Services
{
    public class InfluencerService : IInfluencerService
    {
        private readonly IInfluencerRepository _repo;
        private readonly IMapper _mapper;

        public InfluencerService(IInfluencerRepository repo, IMapper mapper)
        {
            _repo = repo;
            _mapper = mapper;
        }

        public async Task<InfluencerDto> CreateAsync(InfluencerDto dto)
        {
            var entity = _mapper.Map<Influencer>(dto);
            var added = await _repo.AddAsync(entity);
            return _mapper.Map<InfluencerDto>(added);
        }

        public async Task<IEnumerable<InfluencerDto>> GetAllAsync()
        {
            var list = await _repo.GetAllAsync();
            return _mapper.Map<IEnumerable<InfluencerDto>>(list);
        }

        public async Task<InfluencerDto> GetByIdAsync(int id)
        {
            var influencer = await _repo.GetByIdAsync(id);
            return _mapper.Map<InfluencerDto>(influencer);
        }

        public async Task<InfluencerDto> UpdateAsync(int id, InfluencerDto dto)
        {
            var existing = await _repo.GetByIdAsync(id);
            if (existing == null) return null;
            _mapper.Map(dto, existing);
            await _repo.UpdateAsync(existing);
            return _mapper.Map<InfluencerDto>(existing);
        }
    }
}