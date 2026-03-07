using System;
using System.Collections.Generic;

namespace InfluencerMatch.Application.DTOs
{
    public class WorkspaceDto
    {
        public int WorkspaceId { get; set; }
        public string Name { get; set; } = string.Empty;
        public int OwnerUserId { get; set; }
        public string MyRole { get; set; } = string.Empty;
        public int SeatLimit { get; set; }
        public int UsedSeats { get; set; }
        public int PendingInvites { get; set; }
        public int RemainingSeats { get; set; }
        public List<WorkspaceMemberDto> Members { get; set; } = new();
        public List<WorkspaceInviteDto> Invites { get; set; } = new();
        public List<WorkspaceAuditLogDto> AuditLogs { get; set; } = new();
    }

    public class WorkspaceMemberDto
    {
        public int WorkspaceMemberId { get; set; }
        public int UserId { get; set; }
        public string Name { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public string Role { get; set; } = string.Empty;
        public DateTime JoinedAt { get; set; }
    }

    public class WorkspaceInviteDto
    {
        public int WorkspaceInviteId { get; set; }
        public string Email { get; set; } = string.Empty;
        public string Role { get; set; } = string.Empty;
        public string Status { get; set; } = string.Empty;
        public string InviteToken { get; set; } = string.Empty;
        public string InviteUrl { get; set; } = string.Empty;
        public DateTime ExpiresAt { get; set; }
        public DateTime CreatedAt { get; set; }
    }

    public class WorkspaceAuditLogDto
    {
        public int WorkspaceAuditLogId { get; set; }
        public string Action { get; set; } = string.Empty;
        public string Target { get; set; } = string.Empty;
        public string ActorName { get; set; } = string.Empty;
        public string? MetadataJson { get; set; }
        public DateTime CreatedAt { get; set; }
    }

    public class CreateWorkspaceDto
    {
        public string Name { get; set; } = string.Empty;
    }

    public class InviteWorkspaceMemberDto
    {
        public string Email { get; set; } = string.Empty;
        public string Role { get; set; } = "Analyst";
    }

    public class AcceptWorkspaceInviteDto
    {
        public string InviteToken { get; set; } = string.Empty;
    }

    public class UpdateWorkspaceMemberRoleDto
    {
        public string Role { get; set; } = "Analyst";
    }
}
