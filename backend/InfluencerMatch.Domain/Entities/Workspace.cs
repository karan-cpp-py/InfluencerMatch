using System;
using System.Collections.Generic;

namespace InfluencerMatch.Domain.Entities
{
    public class Workspace
    {
        public int WorkspaceId { get; set; }
        public string Name { get; set; } = string.Empty;
        public int OwnerUserId { get; set; }
        public User OwnerUser { get; set; } = null!;
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        public ICollection<WorkspaceMember> Members { get; set; } = new List<WorkspaceMember>();
        public ICollection<WorkspaceInvite> Invites { get; set; } = new List<WorkspaceInvite>();
        public ICollection<WorkspaceAuditLog> AuditLogs { get; set; } = new List<WorkspaceAuditLog>();
    }
}
