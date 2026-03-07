using System;

namespace InfluencerMatch.Domain.Entities
{
    public class WorkspaceAuditLog
    {
        public int WorkspaceAuditLogId { get; set; }
        public int WorkspaceId { get; set; }
        public Workspace Workspace { get; set; } = null!;

        public int ActorUserId { get; set; }
        public User ActorUser { get; set; } = null!;

        public string Action { get; set; } = string.Empty;
        public string Target { get; set; } = string.Empty;
        public string? MetadataJson { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    }
}
