using System.Collections.Generic;
using System.Threading.Tasks;
using InfluencerMatch.Application.DTOs;

namespace InfluencerMatch.Application.Interfaces
{
    public interface IWorkspaceService
    {
        Task<WorkspaceDto?> GetWorkspaceForUserAsync(int userId);
        Task<WorkspaceDto> CreateWorkspaceAsync(int userId, CreateWorkspaceDto dto);
        Task<WorkspaceInviteDto> InviteMemberAsync(int userId, InviteWorkspaceMemberDto dto);
        Task<WorkspaceDto> AcceptInviteAsync(int userId, string userEmail, AcceptWorkspaceInviteDto dto);
        Task<WorkspaceMemberDto> UpdateMemberRoleAsync(int userId, int workspaceMemberId, UpdateWorkspaceMemberRoleDto dto);
        Task RemoveMemberAsync(int userId, int workspaceMemberId);
        Task<IReadOnlyList<WorkspaceInviteDto>> GetPendingInvitesForUserAsync(string email);
    }
}
