using System;

namespace InfluencerMatch.Domain.Entities
{
    public class EnterpriseLeadActivity
    {
        public int EnterpriseLeadActivityId { get; set; }
        public int EnterpriseLeadId { get; set; }
        public EnterpriseLead EnterpriseLead { get; set; } = null!;

        public string ActivityType { get; set; } = "Updated";
        public string Message { get; set; } = string.Empty;

        public int? ActorUserId { get; set; }
        public User? ActorUser { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    }
}
