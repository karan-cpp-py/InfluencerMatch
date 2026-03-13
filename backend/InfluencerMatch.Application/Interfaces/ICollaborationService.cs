using System.Collections.Generic;
using System.Threading.Tasks;
using InfluencerMatch.Application.DTOs;

namespace InfluencerMatch.Application.Interfaces
{
    public interface ICollaborationService
    {
        /// <summary>Brand sends a collaboration request to a registered creator.</summary>
        Task<CollaborationRequestDto> SendRequestAsync(int brandUserId,
            SendCollaborationDto dto);

        /// <summary>Creator accepts a pending request (status → Accepted).</summary>
        Task<CollaborationRequestDto> AcceptRequestAsync(int requestId, int creatorProfileId);

        /// <summary>Creator rejects a pending request (status → Rejected).</summary>
        Task<CollaborationRequestDto> RejectRequestAsync(int requestId, int creatorProfileId);

        /// <summary>All requests sent by a brand.</summary>
        Task<List<CollaborationRequestDto>> GetBrandRequestsAsync(int brandUserId);

        /// <summary>All requests received by a creator.</summary>
        Task<List<CollaborationRequestDto>> GetCreatorRequestsAsync(int creatorProfileId);

        Task<CollaborationWorkflowDto> GetWorkflowAsync(int requestId, int userId, string role);
        Task<CollaborationMilestoneDto> AddMilestoneAsync(int requestId, int actorUserId, AddMilestoneDto dto);
        Task<CollaborationMilestoneDto> UpdateMilestoneStatusAsync(int milestoneId, int actorUserId, string actorRole, UpdateMilestoneStatusDto dto);
        Task<CollaborationMilestoneDto> RequestRevisionAsync(int milestoneId, int actorUserId, MilestoneRevisionDto dto);
        Task<CollaborationRequestDto> AdvanceStageAsync(int requestId, int actorUserId, string actorRole, string nextStatus, string message);
        Task<CollaborationRequestDto> MarkCompletedAsync(int requestId, int actorUserId, string actorRole);
    }
}
