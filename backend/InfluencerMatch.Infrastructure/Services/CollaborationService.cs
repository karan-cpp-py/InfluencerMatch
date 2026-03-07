using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using InfluencerMatch.Application.DTOs;
using InfluencerMatch.Application.Interfaces;
using InfluencerMatch.Domain.Entities;
using InfluencerMatch.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace InfluencerMatch.Infrastructure.Services
{
    public class CollaborationService : ICollaborationService
    {
        private readonly ApplicationDbContext _db;
        private readonly INotificationService _notifications;

        public CollaborationService(ApplicationDbContext db, INotificationService notifications)
        {
            _db = db;
            _notifications = notifications;
        }

        public async Task<CollaborationRequestDto> SendRequestAsync(
            int brandUserId, SendCollaborationDto dto)
        {
            // Verify creator profile exists
            var profile = await _db.CreatorProfiles.FindAsync(dto.CreatorProfileId)
                ?? throw new InvalidOperationException("Creator profile not found.");

            var request = new CollaborationRequest
            {
                BrandUserId      = brandUserId,
                CreatorProfileId = dto.CreatorProfileId,
                CampaignTitle    = dto.CampaignTitle,
                Budget           = dto.Budget,
                Message          = dto.Message,
                Status           = "Pending",
                CreatedAt        = DateTime.UtcNow
            };
            _db.CollaborationRequests.Add(request);
            await _db.SaveChangesAsync();

            await AddActivityAsync(request.RequestId, brandUserId, "Brand", "RequestSent", "Collaboration request sent.");

            var creatorUserId = await _db.CreatorProfiles
                .Where(x => x.CreatorProfileId == request.CreatorProfileId)
                .Select(x => x.UserId)
                .FirstOrDefaultAsync();

            if (creatorUserId > 0)
            {
                await _notifications.NotifyAsync(new NotificationCreateRequestDto
                {
                    UserId = creatorUserId,
                    Type = "collaboration.received",
                    Title = "New collaboration request",
                    Message = $"{request.CampaignTitle} from brand account #{brandUserId}.",
                    SendEmail = true
                });
            }

            await _db.Entry(request).Reference(r => r.Brand).LoadAsync();
            await LoadCreatorChannelName(request);
            return ToDto(request);
        }

        public async Task<CollaborationRequestDto> AcceptRequestAsync(
            int requestId, int creatorProfileId)
            => await RespondAsync(requestId, creatorProfileId, "Accepted");

        public async Task<CollaborationRequestDto> RejectRequestAsync(
            int requestId, int creatorProfileId)
            => await RespondAsync(requestId, creatorProfileId, "Rejected");

        public async Task<List<CollaborationRequestDto>> GetBrandRequestsAsync(int brandUserId)
        {
            var requests = await _db.CollaborationRequests
                .Include(r => r.Brand)
                .Where(r => r.BrandUserId == brandUserId)
                .OrderByDescending(r => r.CreatedAt)
                .ToListAsync();

            foreach (var r in requests) await LoadCreatorChannelName(r);
            return requests.Select(ToDto).ToList();
        }

        public async Task<List<CollaborationRequestDto>> GetCreatorRequestsAsync(int creatorProfileId)
        {
            var requests = await _db.CollaborationRequests
                .Include(r => r.Brand)
                .Where(r => r.CreatorProfileId == creatorProfileId)
                .OrderByDescending(r => r.CreatedAt)
                .ToListAsync();

            foreach (var r in requests) await LoadCreatorChannelName(r);
            return requests.Select(ToDto).ToList();
        }

        public async Task<CollaborationWorkflowDto> GetWorkflowAsync(int requestId, int userId, string role)
        {
            var request = await EnsureRequestAccessAsync(requestId, userId, role);

            await _db.Entry(request).Reference(x => x.Brand).LoadAsync();
            await LoadCreatorChannelName(request);

            var milestones = await _db.CollaborationMilestones
                .Where(x => x.RequestId == requestId)
                .OrderBy(x => x.DueDate ?? DateTime.MaxValue)
                .ThenBy(x => x.CollaborationMilestoneId)
                .Select(x => new CollaborationMilestoneDto
                {
                    CollaborationMilestoneId = x.CollaborationMilestoneId,
                    RequestId = x.RequestId,
                    Title = x.Title,
                    Description = x.Description,
                    DueDate = x.DueDate,
                    Status = x.Status,
                    DeliverableUrl = x.DeliverableUrl,
                    RevisionNotes = x.RevisionNotes,
                    RevisionCount = x.RevisionCount,
                    CreatedAt = x.CreatedAt,
                    UpdatedAt = x.UpdatedAt
                })
                .ToListAsync();

            var activity = await _db.CollaborationActivities
                .Where(x => x.RequestId == requestId)
                .OrderByDescending(x => x.CreatedAt)
                .Take(80)
                .Select(x => new CollaborationActivityDto
                {
                    CollaborationActivityId = x.CollaborationActivityId,
                    RequestId = x.RequestId,
                    ActorRole = x.ActorRole,
                    ActionType = x.ActionType,
                    Message = x.Message,
                    CreatedAt = x.CreatedAt
                })
                .ToListAsync();

            var completed = milestones.Count == 0 ? 0 : milestones.Count(x => x.Status == "Completed" || x.Status == "Approved");
            var completionPercent = milestones.Count == 0 ? 0 : (int)Math.Round((double)completed * 100 / milestones.Count);

            return new CollaborationWorkflowDto
            {
                Request = ToDto(request),
                Milestones = milestones,
                ActivityFeed = activity,
                CompletionPercent = completionPercent,
                IsCompleted = request.Status == "Completed"
            };
        }

        public async Task<CollaborationMilestoneDto> AddMilestoneAsync(int requestId, int actorUserId, AddMilestoneDto dto)
        {
            var request = await _db.CollaborationRequests
                .FirstOrDefaultAsync(x => x.RequestId == requestId)
                ?? throw new InvalidOperationException("Collaboration request not found.");

            if (request.BrandUserId != actorUserId)
            {
                throw new InvalidOperationException("Only request owner can add milestones.");
            }

            var milestone = new CollaborationMilestone
            {
                RequestId = requestId,
                Title = dto.Title,
                Description = dto.Description,
                DueDate = dto.DueDate,
                Status = "Pending",
                CreatedAt = DateTime.UtcNow
            };

            _db.CollaborationMilestones.Add(milestone);
            await _db.SaveChangesAsync();

            await AddActivityAsync(requestId, actorUserId, "Brand", "MilestoneAdded", $"Milestone '{dto.Title}' added.");

            return await ToMilestoneDtoAsync(milestone.CollaborationMilestoneId);
        }

        public async Task<CollaborationMilestoneDto> UpdateMilestoneStatusAsync(int milestoneId, int actorUserId, string actorRole, UpdateMilestoneStatusDto dto)
        {
            var milestone = await _db.CollaborationMilestones
                .Include(x => x.Request)
                .FirstOrDefaultAsync(x => x.CollaborationMilestoneId == milestoneId)
                ?? throw new InvalidOperationException("Milestone not found.");

            await EnsureMilestoneAccessAsync(milestone, actorUserId, actorRole);

            milestone.Status = dto.Status;
            if (!string.IsNullOrWhiteSpace(dto.DeliverableUrl))
            {
                milestone.DeliverableUrl = dto.DeliverableUrl.Trim();
            }

            milestone.UpdatedAt = DateTime.UtcNow;
            await _db.SaveChangesAsync();

            await AddActivityAsync(milestone.RequestId, actorUserId, actorRole, "MilestoneStatus", $"Milestone '{milestone.Title}' changed to {milestone.Status}.");
            await MaybeNotifyCounterpartyAsync(milestone.Request, actorUserId, actorRole, "collaboration.milestone.status", "Milestone status updated", $"{milestone.Title} is now {milestone.Status}.");

            return await ToMilestoneDtoAsync(milestoneId);
        }

        public async Task<CollaborationMilestoneDto> RequestRevisionAsync(int milestoneId, int actorUserId, MilestoneRevisionDto dto)
        {
            var milestone = await _db.CollaborationMilestones
                .Include(x => x.Request)
                .FirstOrDefaultAsync(x => x.CollaborationMilestoneId == milestoneId)
                ?? throw new InvalidOperationException("Milestone not found.");

            if (milestone.Request.BrandUserId != actorUserId)
            {
                throw new InvalidOperationException("Only brand can request revisions.");
            }

            milestone.Status = "RevisionRequested";
            milestone.RevisionNotes = dto.RevisionNotes;
            milestone.RevisionCount += 1;
            milestone.UpdatedAt = DateTime.UtcNow;
            await _db.SaveChangesAsync();

            await AddActivityAsync(milestone.RequestId, actorUserId, "Brand", "RevisionRequested", $"Revision requested for '{milestone.Title}'.");

            var creatorUserId = await _db.CreatorProfiles
                .Where(x => x.CreatorProfileId == milestone.Request.CreatorProfileId)
                .Select(x => x.UserId)
                .FirstOrDefaultAsync();

            if (creatorUserId > 0)
            {
                await _notifications.NotifyAsync(new NotificationCreateRequestDto
                {
                    UserId = creatorUserId,
                    Type = "collaboration.revision.requested",
                    Title = "Revision requested",
                    Message = $"{milestone.Title}: {dto.RevisionNotes}",
                    SendEmail = true
                });
            }

            return await ToMilestoneDtoAsync(milestoneId);
        }

        public async Task<CollaborationRequestDto> MarkCompletedAsync(int requestId, int actorUserId, string actorRole)
        {
            var request = await _db.CollaborationRequests
                .Include(x => x.Brand)
                .FirstOrDefaultAsync(x => x.RequestId == requestId)
                ?? throw new InvalidOperationException("Collaboration request not found.");

            if (request.BrandUserId != actorUserId)
            {
                throw new InvalidOperationException("Only brand can mark collaboration completed.");
            }

            var remaining = await _db.CollaborationMilestones
                .CountAsync(x => x.RequestId == requestId && x.Status != "Completed" && x.Status != "Approved");
            if (remaining > 0)
            {
                throw new InvalidOperationException("Complete or approve all milestones before closing collaboration.");
            }

            request.Status = "Completed";
            request.UpdatedAt = DateTime.UtcNow;
            await _db.SaveChangesAsync();

            await AddActivityAsync(requestId, actorUserId, actorRole, "CollaborationCompleted", "Collaboration marked as completed.");
            await MaybeNotifyCounterpartyAsync(request, actorUserId, actorRole, "collaboration.completed", "Collaboration completed", $"{request.CampaignTitle} has been completed.");

            await LoadCreatorChannelName(request);
            return ToDto(request);
        }

        // ── Helpers ──────────────────────────────────────────────────────────

        private async Task<CollaborationRequestDto> RespondAsync(
            int requestId, int creatorProfileId, string status)
        {
            var request = await _db.CollaborationRequests
                .Include(r => r.Brand)
                .FirstOrDefaultAsync(r => r.RequestId == requestId
                                       && r.CreatorProfileId == creatorProfileId)
                ?? throw new InvalidOperationException("Collaboration request not found.");

            if (request.Status != "Pending")
                throw new InvalidOperationException(
                    $"Cannot change status: request is already {request.Status}.");

            request.Status    = status;
            request.UpdatedAt = DateTime.UtcNow;
            await _db.SaveChangesAsync();

            await AddActivityAsync(request.RequestId, creatorProfileId, "Creator", "RequestDecision", $"Request {status.ToLowerInvariant()} by creator.");

            await _notifications.NotifyAsync(new NotificationCreateRequestDto
            {
                UserId = request.BrandUserId,
                Type = status == "Accepted" ? "collaboration.accepted" : "collaboration.rejected",
                Title = status == "Accepted" ? "Collaboration accepted" : "Collaboration rejected",
                Message = $"{request.CampaignTitle} was {status.ToLowerInvariant()} by creator.",
                SendEmail = true
            });

            await LoadCreatorChannelName(request);
            return ToDto(request);
        }

        private async Task<CollaborationRequest> EnsureRequestAccessAsync(int requestId, int userId, string role)
        {
            var request = await _db.CollaborationRequests
                .FirstOrDefaultAsync(x => x.RequestId == requestId)
                ?? throw new InvalidOperationException("Collaboration request not found.");

            var isBrandSide = role == "Brand" || role == "Agency";
            if (isBrandSide && request.BrandUserId == userId)
            {
                return request;
            }

            if (role == "Creator")
            {
                var profileId = await _db.CreatorProfiles
                    .Where(x => x.UserId == userId)
                    .Select(x => x.CreatorProfileId)
                    .FirstOrDefaultAsync();

                if (profileId == request.CreatorProfileId)
                {
                    return request;
                }
            }

            throw new InvalidOperationException("Access denied for this collaboration workflow.");
        }

        private async Task EnsureMilestoneAccessAsync(CollaborationMilestone milestone, int actorUserId, string actorRole)
        {
            if ((actorRole == "Brand" || actorRole == "Agency") && milestone.Request.BrandUserId == actorUserId)
            {
                return;
            }

            if (actorRole == "Creator")
            {
                var profileId = await _db.CreatorProfiles
                    .Where(x => x.UserId == actorUserId)
                    .Select(x => x.CreatorProfileId)
                    .FirstOrDefaultAsync();
                if (profileId == milestone.Request.CreatorProfileId)
                {
                    return;
                }
            }

            throw new InvalidOperationException("Access denied for this milestone.");
        }

        private async Task AddActivityAsync(int requestId, int? actorUserId, string actorRole, string actionType, string message)
        {
            _db.CollaborationActivities.Add(new CollaborationActivity
            {
                RequestId = requestId,
                ActorUserId = actorUserId,
                ActorRole = actorRole,
                ActionType = actionType,
                Message = message,
                CreatedAt = DateTime.UtcNow
            });
            await _db.SaveChangesAsync();
        }

        private async Task MaybeNotifyCounterpartyAsync(CollaborationRequest request, int actorUserId, string actorRole, string type, string title, string message)
        {
            var targetUserId = request.BrandUserId;
            if (string.Equals(actorRole, "Brand", StringComparison.OrdinalIgnoreCase)
                || string.Equals(actorRole, "Agency", StringComparison.OrdinalIgnoreCase))
            {
                targetUserId = await _db.CreatorProfiles
                    .Where(x => x.CreatorProfileId == request.CreatorProfileId)
                    .Select(x => x.UserId)
                    .FirstOrDefaultAsync();
            }

            if (targetUserId > 0 && targetUserId != actorUserId)
            {
                await _notifications.NotifyAsync(new NotificationCreateRequestDto
                {
                    UserId = targetUserId,
                    Type = type,
                    Title = title,
                    Message = message,
                    SendEmail = true
                });
            }
        }

        private async Task<CollaborationMilestoneDto> ToMilestoneDtoAsync(int milestoneId)
        {
            return await _db.CollaborationMilestones
                .Where(x => x.CollaborationMilestoneId == milestoneId)
                .Select(x => new CollaborationMilestoneDto
                {
                    CollaborationMilestoneId = x.CollaborationMilestoneId,
                    RequestId = x.RequestId,
                    Title = x.Title,
                    Description = x.Description,
                    DueDate = x.DueDate,
                    Status = x.Status,
                    DeliverableUrl = x.DeliverableUrl,
                    RevisionNotes = x.RevisionNotes,
                    RevisionCount = x.RevisionCount,
                    CreatedAt = x.CreatedAt,
                    UpdatedAt = x.UpdatedAt
                })
                .FirstAsync();
        }

        private async Task LoadCreatorChannelName(CollaborationRequest r)
        {
            var ch = await _db.CreatorChannels
                .FirstOrDefaultAsync(c => c.CreatorProfileId == r.CreatorProfileId);
            // ChannelName is stored in the DTO; we attach it via a local helper
            r.Creator ??= new CreatorProfile(); // ensure nav is non-null for DTO mapping
            if (ch != null)
                r.Creator.Channels = new List<CreatorChannel> { ch };
        }

        private static CollaborationRequestDto ToDto(CollaborationRequest r)
        {
            var channelName = r.Creator?.Channels?.FirstOrDefault()?.ChannelName
                           ?? $"Creator #{r.CreatorProfileId}";
            return new CollaborationRequestDto
            {
                RequestId        = r.RequestId,
                BrandUserId      = r.BrandUserId,
                BrandName        = r.Brand?.Name ?? string.Empty,
                CreatorProfileId = r.CreatorProfileId,
                ChannelName      = channelName,
                CampaignTitle    = r.CampaignTitle,
                Budget           = r.Budget,
                Message          = r.Message,
                Status           = r.Status,
                CreatedAt        = r.CreatedAt,
                UpdatedAt        = r.UpdatedAt
            };
        }
    }
}
