using System;

namespace InfluencerMatch.Application.DTOs
{
    public class BookDemoLeadDto
    {
        public string FullName { get; set; } = string.Empty;
        public string WorkEmail { get; set; } = string.Empty;
        public string CompanyName { get; set; } = string.Empty;
        public string? TeamSize { get; set; }
        public string? Notes { get; set; }
        public string Source { get; set; } = "BookDemo";
    }

    public class ReferralSummaryDto
    {
        public string Code { get; set; } = string.Empty;
        public bool IsActive { get; set; }
        public int TotalReferrals { get; set; }
    }

    public class ApplyReferralCodeDto
    {
        public string ReferralCode { get; set; } = string.Empty;
    }

    public class ReferralUsageDto
    {
        public int ReferredUserId { get; set; }
        public string ReferredUserName { get; set; } = string.Empty;
        public string ReferredEmail { get; set; } = string.Empty;
        public DateTime CreatedAt { get; set; }
    }
}
