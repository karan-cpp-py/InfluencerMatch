using System;
using System.Collections.Generic;

namespace InfluencerMatch.Domain.Entities
{
    public class ReferralCode
    {
        public int ReferralCodeId { get; set; }
        public int OwnerUserId { get; set; }
        public User OwnerUser { get; set; } = null!;

        public string Code { get; set; } = string.Empty;
        public bool IsActive { get; set; } = true;
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        public ICollection<ReferralUsage> Usages { get; set; } = new List<ReferralUsage>();
    }
}
