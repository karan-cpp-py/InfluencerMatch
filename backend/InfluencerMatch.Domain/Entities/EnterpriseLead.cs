using System;
using System.Collections.Generic;

namespace InfluencerMatch.Domain.Entities
{
    public class EnterpriseLead
    {
        public int EnterpriseLeadId { get; set; }
        public string FullName { get; set; } = string.Empty;
        public string WorkEmail { get; set; } = string.Empty;
        public string CompanyName { get; set; } = string.Empty;
        public string? TeamSize { get; set; }
        public string? Notes { get; set; }
        public string Source { get; set; } = "BookDemo";
        public string Status { get; set; } = "New";

        public int? OwnerUserId { get; set; }
        public User? OwnerUser { get; set; }

        public int? UserId { get; set; }
        public User? User { get; set; }

        public ICollection<EnterpriseLeadActivity> Activities { get; set; } = new List<EnterpriseLeadActivity>();

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime? UpdatedAt { get; set; }
    }
}
