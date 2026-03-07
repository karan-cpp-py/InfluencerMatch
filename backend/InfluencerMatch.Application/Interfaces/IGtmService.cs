using System.Collections.Generic;
using System.Threading.Tasks;
using InfluencerMatch.Application.DTOs;

namespace InfluencerMatch.Application.Interfaces
{
    public interface IGtmService
    {
        Task<BookDemoLeadDto> BookDemoAsync(int? userId, BookDemoLeadDto dto);
        Task<ReferralSummaryDto> GetOrCreateReferralCodeAsync(int userId);
        Task<IReadOnlyList<ReferralUsageDto>> GetReferralUsageAsync(int userId, int take = 25);
        Task ApplyReferralCodeAsync(int referredUserId, string referralCode);
    }
}
