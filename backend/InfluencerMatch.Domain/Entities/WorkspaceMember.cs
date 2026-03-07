using System;

namespace InfluencerMatch.Domain.Entities
{
    public class WorkspaceMember
    {
        public int WorkspaceMemberId { get; set; }
        public int WorkspaceId { get; set; }
        public Workspace Workspace { get; set; } = null!;

        public int UserId { get; set; }
        public User User { get; set; } = null!;

        // Owner | Manager | Analyst
        public string Role { get; set; } = "Analyst";
        public DateTime JoinedAt { get; set; } = DateTime.UtcNow;
    }
}
