using AutoMapper;
using InfluencerMatch.Application.DTOs;
using InfluencerMatch.Domain.Entities;

namespace InfluencerMatch.Application.Services
{
    public class MappingProfile : Profile
    {
        public MappingProfile()
        {
            CreateMap<UserRegisterDto, User>();
            CreateMap<User, UserRegisterDto>();

            CreateMap<InfluencerDto, Influencer>().ReverseMap();
            CreateMap<CampaignDto, Campaign>().ReverseMap();
            CreateMap<MatchResultDto, Influencer>();
        }
    }
}