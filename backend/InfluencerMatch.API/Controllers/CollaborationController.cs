using System.Security.Claims;
using System.Threading.Tasks;
using InfluencerMatch.Application.DTOs;
using InfluencerMatch.Application.Interfaces;
using InfluencerMatch.Infrastructure.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace InfluencerMatch.API.Controllers
{
    /// <summary>
    /// Collaboration request endpoints.
    ///
    /// Brand:
    ///   POST /api/collaborations     — send a request to a registered creator
    ///   GET  /api/collaborations     — list requests sent by this brand
    ///
    /// Creator (via GET /api/collaborations when Role = "Creator"):
    ///   GET  /api/collaborations     — list requests received by this creator
    ///   (Accept/Reject are on CreatorController for cleaner routing)
    /// </summary>
    [ApiController]
    [Route("api/collaborations")]
    [Authorize]
    public class CollaborationController : ControllerBase
    {
        private readonly ICollaborationService _collaboration;
        private readonly ApplicationDbContext  _db;

        public CollaborationController(
            ICollaborationService collaboration,
            ApplicationDbContext  db)
        {
            _collaboration = collaboration;
            _db            = db;
        }

        /// <summary>Brand sends a collaboration request to a creator.</summary>
        [HttpPost]
        [Authorize(Roles = "Brand,Agency")]
        public async Task<IActionResult> SendRequest([FromBody] SendCollaborationDto dto)
        {
            var brandUserId = int.Parse(
                User.FindFirst(ClaimTypes.NameIdentifier)!.Value);
            try
            {
                var result = await _collaboration.SendRequestAsync(brandUserId, dto);
                return Ok(result);
            }
            catch (System.InvalidOperationException ex)
            {
                return BadRequest(new { error = ex.Message });
            }
        }

        /// <summary>
        /// Get collaboration requests.
        /// Brands get requests they sent; creators get requests they received.
        /// </summary>
        [HttpGet]
        public async Task<IActionResult> GetRequests()
        {
            var userId = int.Parse(
                User.FindFirst(ClaimTypes.NameIdentifier)!.Value);
            var role = User.FindFirst(ClaimTypes.Role)?.Value;

            if (role == "Brand" || role == "Agency")
            {
                var results = await _collaboration.GetBrandRequestsAsync(userId);
                return Ok(results);
            }

            if (role == "Creator")
            {
                var profile = await _db.CreatorProfiles
                    .FirstOrDefaultAsync(p => p.UserId == userId);
                if (profile == null)
                    return NotFound(new { error = "Creator profile not found." });

                var results = await _collaboration
                    .GetCreatorRequestsAsync(profile.CreatorProfileId);
                return Ok(results);
            }

            return Forbid();
        }

        [HttpGet("{requestId:int}/workflow")]
        public async Task<IActionResult> GetWorkflow(int requestId)
        {
            try
            {
                var userId = int.Parse(User.FindFirst(ClaimTypes.NameIdentifier)!.Value);
                var role = User.FindFirst(ClaimTypes.Role)?.Value ?? string.Empty;
                var result = await _collaboration.GetWorkflowAsync(requestId, userId, role);
                return Ok(result);
            }
            catch (System.InvalidOperationException ex)
            {
                return BadRequest(new { error = ex.Message });
            }
        }

        [HttpPost("{requestId:int}/milestones")]
        [Authorize(Roles = "Brand,Agency")]
        public async Task<IActionResult> AddMilestone(int requestId, [FromBody] AddMilestoneDto dto)
        {
            try
            {
                var userId = int.Parse(User.FindFirst(ClaimTypes.NameIdentifier)!.Value);
                var result = await _collaboration.AddMilestoneAsync(requestId, userId, dto);
                return Ok(result);
            }
            catch (System.InvalidOperationException ex)
            {
                return BadRequest(new { error = ex.Message });
            }
            catch (DbUpdateException)
            {
                return StatusCode(500, new { error = "Database update failed while adding milestone. Ensure latest migrations are applied." });
            }
        }

        [HttpPatch("milestones/{milestoneId:int}/status")]
        [Authorize(Roles = "Brand,Agency,Creator")]
        public async Task<IActionResult> UpdateMilestoneStatus(int milestoneId, [FromBody] UpdateMilestoneStatusDto dto)
        {
            try
            {
                var userId = int.Parse(User.FindFirst(ClaimTypes.NameIdentifier)!.Value);
                var role = User.FindFirst(ClaimTypes.Role)?.Value ?? string.Empty;
                var result = await _collaboration.UpdateMilestoneStatusAsync(milestoneId, userId, role, dto);
                return Ok(result);
            }
            catch (System.InvalidOperationException ex)
            {
                return BadRequest(new { error = ex.Message });
            }
            catch (DbUpdateException)
            {
                return StatusCode(500, new { error = "Database update failed while updating milestone status. Ensure latest migrations are applied." });
            }
        }

        [HttpPost("milestones/{milestoneId:int}/revision")]
        [Authorize(Roles = "Brand,Agency")]
        public async Task<IActionResult> RequestRevision(int milestoneId, [FromBody] MilestoneRevisionDto dto)
        {
            try
            {
                var userId = int.Parse(User.FindFirst(ClaimTypes.NameIdentifier)!.Value);
                var result = await _collaboration.RequestRevisionAsync(milestoneId, userId, dto);
                return Ok(result);
            }
            catch (System.InvalidOperationException ex)
            {
                return BadRequest(new { error = ex.Message });
            }
            catch (DbUpdateException)
            {
                return StatusCode(500, new { error = "Database update failed while requesting revision. Ensure latest migrations are applied." });
            }
        }

        [HttpPost("{requestId:int}/complete")]
        [Authorize(Roles = "Brand,Agency")]
        public async Task<IActionResult> CompleteCollaboration(int requestId)
        {
            try
            {
                var userId = int.Parse(User.FindFirst(ClaimTypes.NameIdentifier)!.Value);
                var role = User.FindFirst(ClaimTypes.Role)?.Value ?? string.Empty;
                var result = await _collaboration.MarkCompletedAsync(requestId, userId, role);
                return Ok(result);
            }
            catch (System.InvalidOperationException ex)
            {
                return BadRequest(new { error = ex.Message });
            }
        }

        [HttpPost("{requestId:int}/proposal")]
        [Authorize(Roles = "Brand,Agency")]
        public async Task<IActionResult> SendProposal(int requestId, [FromBody] CollaborationStageUpdateDto dto)
        {
            try
            {
                var userId = int.Parse(User.FindFirst(ClaimTypes.NameIdentifier)!.Value);
                var role = User.FindFirst(ClaimTypes.Role)?.Value ?? string.Empty;
                var note = string.IsNullOrWhiteSpace(dto.Notes)
                    ? "Brand shared a formal proposal."
                    : dto.Notes.Trim();

                var result = await _collaboration.AdvanceStageAsync(requestId, userId, role, "ProposalSent", note);
                return Ok(result);
            }
            catch (System.InvalidOperationException ex)
            {
                return BadRequest(new { error = ex.Message });
            }
        }

        [HttpPost("{requestId:int}/contract")]
        [Authorize(Roles = "Brand,Agency,Creator")]
        public async Task<IActionResult> UpdateContract(int requestId, [FromBody] CollaborationStageUpdateDto dto)
        {
            try
            {
                var userId = int.Parse(User.FindFirst(ClaimTypes.NameIdentifier)!.Value);
                var role = User.FindFirst(ClaimTypes.Role)?.Value ?? string.Empty;
                var isCreator = string.Equals(role, "Creator", System.StringComparison.OrdinalIgnoreCase);

                var status = isCreator ? "ContractSigned" : "ContractDrafted";
                var note = string.IsNullOrWhiteSpace(dto.Notes)
                    ? (isCreator ? "Creator signed the contract." : "Brand uploaded a draft contract.")
                    : dto.Notes.Trim();

                var result = await _collaboration.AdvanceStageAsync(requestId, userId, role, status, note);
                return Ok(result);
            }
            catch (System.InvalidOperationException ex)
            {
                return BadRequest(new { error = ex.Message });
            }
        }

        [HttpPost("{requestId:int}/payment-release")]
        [Authorize(Roles = "Brand,Agency")]
        public async Task<IActionResult> ReleasePayment(int requestId, [FromBody] CollaborationStageUpdateDto dto)
        {
            try
            {
                var userId = int.Parse(User.FindFirst(ClaimTypes.NameIdentifier)!.Value);
                var role = User.FindFirst(ClaimTypes.Role)?.Value ?? string.Empty;
                var note = string.IsNullOrWhiteSpace(dto.Notes)
                    ? "Brand marked payment as released for this collaboration."
                    : dto.Notes.Trim();

                var result = await _collaboration.AdvanceStageAsync(requestId, userId, role, "PaymentReleased", note);
                return Ok(result);
            }
            catch (System.InvalidOperationException ex)
            {
                return BadRequest(new { error = ex.Message });
            }
        }
    }
}
