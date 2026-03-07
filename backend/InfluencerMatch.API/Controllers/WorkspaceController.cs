using System;
using System.Security.Claims;
using System.Threading.Tasks;
using InfluencerMatch.Application.DTOs;
using InfluencerMatch.Application.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace InfluencerMatch.API.Controllers
{
    [ApiController]
    [Route("api/workspace")]
    [Authorize(Roles = "Brand,Agency")]
    public class WorkspaceController : ControllerBase
    {
        private readonly IWorkspaceService _workspaceService;

        public WorkspaceController(IWorkspaceService workspaceService)
        {
            _workspaceService = workspaceService;
        }

        [HttpGet]
        public async Task<IActionResult> GetMyWorkspace()
        {
            var workspace = await _workspaceService.GetWorkspaceForUserAsync(GetUserId());
            if (workspace == null)
            {
                return Ok(new { hasWorkspace = false });
            }

            return Ok(new { hasWorkspace = true, workspace });
        }

        [HttpPost]
        public async Task<IActionResult> CreateWorkspace([FromBody] CreateWorkspaceDto dto)
        {
            try
            {
                var result = await _workspaceService.CreateWorkspaceAsync(GetUserId(), dto);
                return Ok(result);
            }
            catch (InvalidOperationException ex)
            {
                return BadRequest(new { error = ex.Message });
            }
        }

        [HttpPost("invites")]
        public async Task<IActionResult> Invite([FromBody] InviteWorkspaceMemberDto dto)
        {
            try
            {
                var result = await _workspaceService.InviteMemberAsync(GetUserId(), dto);
                return Ok(result);
            }
            catch (InvalidOperationException ex)
            {
                return BadRequest(new { error = ex.Message });
            }
        }

        [HttpGet("my-invites")]
        public async Task<IActionResult> MyInvites()
        {
            var email = User.FindFirstValue(ClaimTypes.Email) ?? string.Empty;
            var rows = await _workspaceService.GetPendingInvitesForUserAsync(email);
            return Ok(rows);
        }

        [HttpPost("invites/accept")]
        public async Task<IActionResult> Accept([FromBody] AcceptWorkspaceInviteDto dto)
        {
            try
            {
                var result = await _workspaceService.AcceptInviteAsync(
                    GetUserId(),
                    User.FindFirstValue(ClaimTypes.Email) ?? string.Empty,
                    dto);
                return Ok(result);
            }
            catch (InvalidOperationException ex)
            {
                return BadRequest(new { error = ex.Message });
            }
        }

        [HttpPatch("members/{workspaceMemberId:int}/role")]
        public async Task<IActionResult> UpdateRole(int workspaceMemberId, [FromBody] UpdateWorkspaceMemberRoleDto dto)
        {
            try
            {
                var result = await _workspaceService.UpdateMemberRoleAsync(GetUserId(), workspaceMemberId, dto);
                return Ok(result);
            }
            catch (InvalidOperationException ex)
            {
                return BadRequest(new { error = ex.Message });
            }
        }

        [HttpDelete("members/{workspaceMemberId:int}")]
        public async Task<IActionResult> RemoveMember(int workspaceMemberId)
        {
            try
            {
                await _workspaceService.RemoveMemberAsync(GetUserId(), workspaceMemberId);
                return Ok(new { ok = true });
            }
            catch (InvalidOperationException ex)
            {
                return BadRequest(new { error = ex.Message });
            }
        }

        private int GetUserId() => int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
    }
}
