using System.Collections.Generic;
using System.Threading.Tasks;
using InfluencerMatch.Application.DTOs;

namespace InfluencerMatch.Application.Interfaces
{
    public interface IMatchService
    {
        Task<IEnumerable<MatchResultDto>> MatchCampaignAsync(int campaignId, bool includeOverBudget = false);
    }
}