using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using InfluencerMatch.Application.DTOs;
using InfluencerMatch.Application.Interfaces;
using InfluencerMatch.Domain.Entities;
using InfluencerMatch.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;

namespace InfluencerMatch.Infrastructure.Services
{
    public class WorkspaceService : IWorkspaceService
    {
        private static readonly HashSet<string> TeamRoles = new(StringComparer.OrdinalIgnoreCase)
        {
            "Owner", "Manager", "Analyst"
        };

        private readonly ApplicationDbContext _db;
        private readonly INotificationService _notifications;
        private readonly string _frontendBaseUrl;

        public WorkspaceService(ApplicationDbContext db, INotificationService notifications, IConfiguration configuration)
        {
            _db = db;
            _notifications = notifications;
            _frontendBaseUrl = (configuration["App:FrontendBaseUrl"] ?? "http://localhost:5173").TrimEnd('/');
        }

        public async Task<WorkspaceDto?> GetWorkspaceForUserAsync(int userId)
        {
            var workspace = await _db.WorkspaceMembers
                .AsNoTracking()
                .Where(m => m.UserId == userId)
                .Select(m => m.Workspace)
                .Include(w => w.Members).ThenInclude(m => m.User)
                .Include(w => w.Invites.Where(i => i.Status == "Pending"))
                .Include(w => w.AuditLogs.OrderByDescending(a => a.CreatedAt).Take(25)).ThenInclude(a => a.ActorUser)
                .FirstOrDefaultAsync();

            if (workspace == null)
            {
                return null;
            }

            var myRole = workspace.Members.FirstOrDefault(m => m.UserId == userId)?.Role ?? string.Empty;
            var seatLimit = (await ResolveWorkspaceSeatLimitAsync(workspace.WorkspaceId)) ?? 1;
            return MapWorkspace(workspace, myRole, seatLimit);
        }

        public async Task<WorkspaceDto> CreateWorkspaceAsync(int userId, CreateWorkspaceDto dto)
        {
            var user = await _db.Users.AsNoTracking().FirstOrDefaultAsync(x => x.UserId == userId);
            if (user == null)
            {
                throw new InvalidOperationException("User not found.");
            }

            if (user.Role != "Brand" && user.Role != "Agency")
            {
                throw new InvalidOperationException("Only Brand and Agency users can create team workspaces.");
            }

            var existing = await _db.WorkspaceMembers.AsNoTracking().AnyAsync(x => x.UserId == userId);
            if (existing)
            {
                throw new InvalidOperationException("User is already part of a workspace.");
            }

            var name = string.IsNullOrWhiteSpace(dto.Name)
                ? $"{(string.IsNullOrWhiteSpace(user.CompanyName) ? user.Name : user.CompanyName)} Workspace"
                : dto.Name.Trim();

            var workspace = new Workspace
            {
                Name = name,
                OwnerUserId = userId,
                CreatedAt = DateTime.UtcNow
            };

            _db.Workspaces.Add(workspace);
            await _db.SaveChangesAsync();

            _db.WorkspaceMembers.Add(new WorkspaceMember
            {
                WorkspaceId = workspace.WorkspaceId,
                UserId = userId,
                Role = "Owner",
                JoinedAt = DateTime.UtcNow
            });

            await WriteAudit(workspace.WorkspaceId, userId, "workspace.created", "workspace", $"{{\"name\":\"{Escape(name)}\"}}");
            await _db.SaveChangesAsync();

            return (await GetWorkspaceForUserAsync(userId))!;
        }

        public async Task<WorkspaceInviteDto> InviteMemberAsync(int userId, InviteWorkspaceMemberDto dto)
        {
            var membership = await GetMembershipOrThrow(userId);
            EnsureCanManageMembers(membership.Role);

            var email = (dto.Email ?? string.Empty).Trim().ToLowerInvariant();
            if (string.IsNullOrWhiteSpace(email))
            {
                throw new InvalidOperationException("Invite email is required.");
            }

            var role = NormalizeTeamRole(dto.Role, allowOwner: false);

            var seatLimit = await ResolveWorkspaceSeatLimitAsync(membership.WorkspaceId);
            if (seatLimit.HasValue)
            {
                var activeSeats = await _db.WorkspaceMembers.CountAsync(x => x.WorkspaceId == membership.WorkspaceId);
                var pendingSeats = await _db.WorkspaceInvites.CountAsync(x => x.WorkspaceId == membership.WorkspaceId && x.Status == "Pending" && x.ExpiresAt > DateTime.UtcNow);
                if (activeSeats + pendingSeats >= seatLimit.Value)
                {
                    throw new InvalidOperationException($"Seat limit reached for your current plan ({seatLimit.Value} seats). Upgrade to add more team members.");
                }
            }

            var existingUser = await _db.Users.AsNoTracking().FirstOrDefaultAsync(x => x.Email.ToLower() == email);
            if (existingUser != null)
            {
                var alreadyMember = await _db.WorkspaceMembers.AsNoTracking().AnyAsync(x => x.WorkspaceId == membership.WorkspaceId && x.UserId == existingUser.UserId);
                if (alreadyMember)
                {
                    throw new InvalidOperationException("User is already in this workspace.");
                }
            }

            var pending = await _db.WorkspaceInvites.FirstOrDefaultAsync(i =>
                i.WorkspaceId == membership.WorkspaceId
                && i.Email == email
                && i.Status == "Pending"
                && i.ExpiresAt > DateTime.UtcNow);

            if (pending != null)
            {
                pending.Role = role;
                pending.ExpiresAt = DateTime.UtcNow.AddDays(7);
                await WriteAudit(membership.WorkspaceId, userId, "workspace.invite.updated", email, $"{{\"role\":\"{role}\"}}");
                await _db.SaveChangesAsync();

                var updatedInviteUrl = BuildInviteUrl(pending.InviteToken);
                await _notifications.SendEmailAsync(
                    email,
                    "Your InfluencerMatch workspace invite was updated",
                    $"Your invite role is now {role}. Join here: {updatedInviteUrl}\n\nThis link expires on {pending.ExpiresAt:u}.",
                    "workspace.invite");

                return MapInvite(pending);
            }

            var invite = new WorkspaceInvite
            {
                WorkspaceId = membership.WorkspaceId,
                Email = email,
                Role = role,
                InviteToken = Guid.NewGuid().ToString("N"),
                Status = "Pending",
                InvitedByUserId = userId,
                ExpiresAt = DateTime.UtcNow.AddDays(7),
                CreatedAt = DateTime.UtcNow
            };

            _db.WorkspaceInvites.Add(invite);
            await WriteAudit(membership.WorkspaceId, userId, "workspace.invite.created", email, $"{{\"role\":\"{role}\"}}");
            await _db.SaveChangesAsync();

            var inviteUrl = BuildInviteUrl(invite.InviteToken);
            await _notifications.SendEmailAsync(
                email,
                "You're invited to an InfluencerMatch workspace",
                $"You have been invited as {role}. Click to join: {inviteUrl}\n\nThis link expires on {invite.ExpiresAt:u}.",
                "workspace.invite");

            return MapInvite(invite);
        }

        public async Task<WorkspaceDto> AcceptInviteAsync(int userId, string userEmail, AcceptWorkspaceInviteDto dto)
        {
            var token = (dto.InviteToken ?? string.Empty).Trim();
            if (string.IsNullOrWhiteSpace(token))
            {
                throw new InvalidOperationException("Invite token is required.");
            }

            var invite = await _db.WorkspaceInvites
                .FirstOrDefaultAsync(x => x.InviteToken == token);

            if (invite == null)
            {
                throw new InvalidOperationException("Invite not found.");
            }

            if (!string.Equals(invite.Email, userEmail.Trim(), StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidOperationException("Invite email does not match current account.");
            }

            if (invite.Status != "Pending")
            {
                throw new InvalidOperationException("Invite is no longer valid.");
            }

            if (invite.ExpiresAt <= DateTime.UtcNow)
            {
                invite.Status = "Expired";
                await _db.SaveChangesAsync();
                throw new InvalidOperationException("Invite has expired.");
            }

            var existingWorkspace = await _db.WorkspaceMembers.AsNoTracking().AnyAsync(x => x.UserId == userId);
            if (existingWorkspace)
            {
                throw new InvalidOperationException("User is already part of another workspace.");
            }

            _db.WorkspaceMembers.Add(new WorkspaceMember
            {
                WorkspaceId = invite.WorkspaceId,
                UserId = userId,
                Role = invite.Role,
                JoinedAt = DateTime.UtcNow
            });

            invite.Status = "Accepted";
            invite.AcceptedByUserId = userId;
            invite.AcceptedAt = DateTime.UtcNow;

            await WriteAudit(invite.WorkspaceId, userId, "workspace.invite.accepted", invite.Email, $"{{\"role\":\"{invite.Role}\"}}");
            await _db.SaveChangesAsync();

            return (await GetWorkspaceForUserAsync(userId))!;
        }

        public async Task<WorkspaceMemberDto> UpdateMemberRoleAsync(int userId, int workspaceMemberId, UpdateWorkspaceMemberRoleDto dto)
        {
            var membership = await GetMembershipOrThrow(userId);
            EnsureCanManageMembers(membership.Role);

            var member = await _db.WorkspaceMembers
                .Include(x => x.User)
                .FirstOrDefaultAsync(x => x.WorkspaceMemberId == workspaceMemberId && x.WorkspaceId == membership.WorkspaceId);

            if (member == null)
            {
                throw new InvalidOperationException("Workspace member not found.");
            }

            if (member.Role == "Owner")
            {
                throw new InvalidOperationException("Owner role cannot be changed.");
            }

            var targetRole = NormalizeTeamRole(dto.Role, allowOwner: false);
            member.Role = targetRole;

            await WriteAudit(membership.WorkspaceId, userId, "workspace.member.role_updated", member.User.Email, $"{{\"role\":\"{targetRole}\"}}");
            await _db.SaveChangesAsync();

            return new WorkspaceMemberDto
            {
                WorkspaceMemberId = member.WorkspaceMemberId,
                UserId = member.UserId,
                Name = member.User.Name,
                Email = member.User.Email,
                Role = member.Role,
                JoinedAt = member.JoinedAt
            };
        }

        public async Task RemoveMemberAsync(int userId, int workspaceMemberId)
        {
            var membership = await GetMembershipOrThrow(userId);
            EnsureCanManageMembers(membership.Role);

            var member = await _db.WorkspaceMembers
                .Include(x => x.User)
                .FirstOrDefaultAsync(x => x.WorkspaceMemberId == workspaceMemberId && x.WorkspaceId == membership.WorkspaceId);

            if (member == null)
            {
                throw new InvalidOperationException("Workspace member not found.");
            }

            if (member.Role == "Owner")
            {
                throw new InvalidOperationException("Owner cannot be removed from workspace.");
            }

            _db.WorkspaceMembers.Remove(member);
            await WriteAudit(membership.WorkspaceId, userId, "workspace.member.removed", member.User.Email, null);
            await _db.SaveChangesAsync();
        }

        public async Task<IReadOnlyList<WorkspaceInviteDto>> GetPendingInvitesForUserAsync(string email)
        {
            var normalizedEmail = (email ?? string.Empty).Trim().ToLowerInvariant();
            if (string.IsNullOrWhiteSpace(normalizedEmail))
            {
                return Array.Empty<WorkspaceInviteDto>();
            }

            var rows = await _db.WorkspaceInvites
                .AsNoTracking()
                .Where(x => x.Email == normalizedEmail && x.Status == "Pending" && x.ExpiresAt > DateTime.UtcNow)
                .OrderByDescending(x => x.CreatedAt)
                .Take(25)
                .ToListAsync();

            return rows.Select(MapInvite).ToList();
        }

        private async Task<WorkspaceMember> GetMembershipOrThrow(int userId)
        {
            var membership = await _db.WorkspaceMembers.FirstOrDefaultAsync(x => x.UserId == userId);
            if (membership == null)
            {
                throw new InvalidOperationException("User is not part of any workspace.");
            }

            return membership;
        }

        private static void EnsureCanManageMembers(string role)
        {
            if (!string.Equals(role, "Owner", StringComparison.OrdinalIgnoreCase)
                && !string.Equals(role, "Manager", StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidOperationException("Only Owner or Manager can manage workspace members.");
            }
        }

        private static string NormalizeTeamRole(string? role, bool allowOwner)
        {
            var normalized = (role ?? string.Empty).Trim();
            if (!TeamRoles.Contains(normalized))
            {
                throw new InvalidOperationException("Invalid workspace role. Allowed: Owner, Manager, Analyst.");
            }

            if (!allowOwner && string.Equals(normalized, "Owner", StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidOperationException("Owner role cannot be assigned via invite.");
            }

            if (string.Equals(normalized, "Manager", StringComparison.OrdinalIgnoreCase)) return "Manager";
            if (string.Equals(normalized, "Analyst", StringComparison.OrdinalIgnoreCase)) return "Analyst";
            return "Owner";
        }

        private WorkspaceDto MapWorkspace(Workspace workspace, string myRole, int seatLimit)
        {
            var usedSeats = workspace.Members.Count;
            var pendingInvites = workspace.Invites.Count(i => i.Status == "Pending" && i.ExpiresAt > DateTime.UtcNow);
            var remainingSeats = Math.Max(0, seatLimit - (usedSeats + pendingInvites));

            return new WorkspaceDto
            {
                WorkspaceId = workspace.WorkspaceId,
                Name = workspace.Name,
                OwnerUserId = workspace.OwnerUserId,
                MyRole = myRole,
                SeatLimit = seatLimit,
                UsedSeats = usedSeats,
                PendingInvites = pendingInvites,
                RemainingSeats = remainingSeats,
                Members = workspace.Members
                    .OrderByDescending(m => m.Role == "Owner")
                    .ThenBy(m => m.User.Name)
                    .Select(m => new WorkspaceMemberDto
                    {
                        WorkspaceMemberId = m.WorkspaceMemberId,
                        UserId = m.UserId,
                        Name = m.User.Name,
                        Email = m.User.Email,
                        Role = m.Role,
                        JoinedAt = m.JoinedAt
                    }).ToList(),
                Invites = workspace.Invites
                    .OrderByDescending(i => i.CreatedAt)
                    .Select(i => MapInvite(i))
                    .ToList(),
                AuditLogs = workspace.AuditLogs
                    .OrderByDescending(a => a.CreatedAt)
                    .Select(a => new WorkspaceAuditLogDto
                    {
                        WorkspaceAuditLogId = a.WorkspaceAuditLogId,
                        Action = a.Action,
                        Target = a.Target,
                        ActorName = a.ActorUser?.Name ?? "System",
                        MetadataJson = a.MetadataJson,
                        CreatedAt = a.CreatedAt
                    }).ToList()
            };
        }

        private WorkspaceInviteDto MapInvite(WorkspaceInvite invite)
        {
            return new WorkspaceInviteDto
            {
                WorkspaceInviteId = invite.WorkspaceInviteId,
                Email = invite.Email,
                Role = invite.Role,
                Status = invite.Status,
                InviteToken = invite.InviteToken,
                InviteUrl = BuildInviteUrl(invite.InviteToken),
                ExpiresAt = invite.ExpiresAt,
                CreatedAt = invite.CreatedAt
            };
        }

        private async Task<int?> ResolveWorkspaceSeatLimitAsync(int workspaceId)
        {
            var ownerUserId = await _db.Workspaces
                .AsNoTracking()
                .Where(x => x.WorkspaceId == workspaceId)
                .Select(x => x.OwnerUserId)
                .FirstOrDefaultAsync();

            if (ownerUserId <= 0)
            {
                return 1;
            }

            var now = DateTime.UtcNow;
            var planName = await _db.UserSubscriptions
                .AsNoTracking()
                .Where(s => s.UserId == ownerUserId && s.Status == "Active" && s.EndDate > now)
                .OrderByDescending(s => s.CreatedAt)
                .Select(s => s.Plan.PlanName)
                .FirstOrDefaultAsync();

            if (string.IsNullOrWhiteSpace(planName))
            {
                return 1;
            }

            return planName.Trim().ToLowerInvariant() switch
            {
                "free" => 1,
                "starter" => 3,
                "professional" => 10,
                "enterprise" => 50,
                _ => 3,
            };
        }

        private string BuildInviteUrl(string token)
            => $"{_frontendBaseUrl}/workspace/team?inviteToken={Uri.EscapeDataString(token)}";

        private async Task WriteAudit(int workspaceId, int actorUserId, string action, string target, string? metadataJson)
        {
            _db.WorkspaceAuditLogs.Add(new WorkspaceAuditLog
            {
                WorkspaceId = workspaceId,
                ActorUserId = actorUserId,
                Action = action,
                Target = target,
                MetadataJson = metadataJson,
                CreatedAt = DateTime.UtcNow
            });

            await Task.CompletedTask;
        }

        private static string Escape(string value)
            => value.Replace("\\", "\\\\", StringComparison.Ordinal).Replace("\"", "\\\"", StringComparison.Ordinal);
    }
}
