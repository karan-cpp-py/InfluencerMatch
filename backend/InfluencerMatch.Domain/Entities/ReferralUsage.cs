using System;

namespace InfluencerMatch.Domain.Entities
{
    public class ReferralUsage
    {
        public int ReferralUsageId { get; set; }

        public int ReferralCodeId { get; set; }
        public ReferralCode ReferralCode { get; set; } = null!;

        public int ReferredUserId { get; set; }
        public User ReferredUser { get; set; } = null!;

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    }
}
