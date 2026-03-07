using System;

namespace InfluencerMatch.Domain.Entities
{
    public class WorkspaceInvite
    {
        public int WorkspaceInviteId { get; set; }
        public int WorkspaceId { get; set; }
        public Workspace Workspace { get; set; } = null!;

        public string Email { get; set; } = string.Empty;

        // Owner | Manager | Analyst (Owner invite is blocked at service level)
        public string Role { get; set; } = "Analyst";

        public string InviteToken { get; set; } = string.Empty;

        // Pending | Accepted | Revoked | Expired
        public string Status { get; set; } = "Pending";

        public int InvitedByUserId { get; set; }
        public User InvitedByUser { get; set; } = null!;

        public int? AcceptedByUserId { get; set; }
        public User? AcceptedByUser { get; set; }

        public DateTime ExpiresAt { get; set; }
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime? AcceptedAt { get; set; }
    }
}
