using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using System.Threading;
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
    /// Creator self-service endpoints: registration, channel linking, dashboard.
    ///
    /// Public:
    ///   POST /api/creator/register        — create account + profile
    ///
    /// Requires Role = "Creator":
    ///   GET  /api/creator/profile         — own profile
    ///   PUT  /api/creator/profile         — update profile
    ///   POST /api/creator/link-channel    — link YouTube channel by URL
    ///   GET  /api/creator/channel         — own channel info
    ///   GET  /api/creator/dashboard       — stats snapshot
    ///   GET  /api/creator/collaborations                     — incoming requests
    ///   PATCH /api/creator/collaborations/{id}/accept
    ///   PATCH /api/creator/collaborations/{id}/reject
    /// </summary>
    [ApiController]
    [Route("api/creator")]
    public class CreatorController : ControllerBase
    {
        private readonly ICreatorRegistrationService _registration;
        private readonly ICreatorChannelService      _channel;
        private readonly ICollaborationService       _collaboration;
        private readonly IAdvancedAnalyticsService   _advancedAnalytics;
        private readonly ApplicationDbContext        _db;

        public CreatorController(
            ICreatorRegistrationService registration,
            ICreatorChannelService      channel,
            ICollaborationService       collaboration,
            IAdvancedAnalyticsService   advancedAnalytics,
            ApplicationDbContext        db)
        {
            _registration  = registration;
            _channel       = channel;
            _collaboration = collaboration;
            _advancedAnalytics = advancedAnalytics;
            _db            = db;
        }

        // ── Registration ─────────────────────────────────────────────────────

        /// <summary>Register a new creator account. Returns a JWT token.</summary>
        [HttpPost("register")]
        [AllowAnonymous]
        public async Task<IActionResult> Register([FromBody] CreatorRegisterRequestDto dto)
        {
            try
            {
                var result = await _registration.RegisterCreatorAsync(dto);
                return Ok(result);
            }
            catch (InvalidOperationException ex)
            {
                return Conflict(new { error = ex.Message });
            }
        }

        // ── Profile ──────────────────────────────────────────────────────────

        /// <summary>Get the authenticated creator's profile.</summary>
        [HttpGet("profile")]
        [Authorize(Roles = "Creator")]
        public async Task<IActionResult> GetProfile()
        {
            var profile = await _registration.GetProfileAsync(GetUserId());
            if (profile == null) return NotFound();
            return Ok(profile);
        }

        /// <summary>Update mutable profile fields.</summary>
        [HttpPut("profile")]
        [Authorize(Roles = "Creator")]
        public async Task<IActionResult> UpdateProfile([FromBody] UpdateCreatorProfileDto dto)
        {
            try
            {
                var result = await _registration.UpdateProfileAsync(GetUserId(), dto);
                return Ok(result);
            }
            catch (InvalidOperationException ex)
            {
                return NotFound(new { error = ex.Message });
            }
        }

        // ── Channel Linking ───────────────────────────────────────────────────

        /// <summary>
        /// Link a YouTube channel. Accepts:
        ///   https://youtube.com/channel/UCxxx
        ///   https://youtube.com/@handle
        ///   https://youtube.com/c/name
        /// </summary>
        [HttpPost("link-channel")]
        [Authorize(Roles = "Creator")]
        public async Task<IActionResult> LinkChannel(
            [FromBody] LinkChannelRequestDto dto,
            CancellationToken ct)
        {
            try
            {
                var profileId = await GetCreatorProfileIdAsync();
                var result    = await _channel.LinkChannelAsync(profileId, dto.ChannelUrl, ct);
                return Ok(result);
            }
            catch (InvalidOperationException ex)
            {
                return Conflict(new { error = ex.Message });
            }
            catch (Exception ex) when (
                ex.Message.Contains("quota", StringComparison.OrdinalIgnoreCase))
            {
                return StatusCode(429, new { error = ex.Message });
            }
        }

        /// <summary>Get the creator's linked YouTube channel.</summary>
        [HttpGet("channel")]
        [Authorize(Roles = "Creator")]
        public async Task<IActionResult> GetChannel(CancellationToken ct)
        {
            var profileId = await GetCreatorProfileIdAsync();
            var result    = await _channel.GetChannelAsync(profileId, ct);
            if (result == null)
                return NotFound(new { error = "No channel linked yet." });
            return Ok(result);
        }

        // ── Dashboard ────────────────────────────────────────────────────────

        /// <summary>Creator dashboard: profile + channel stats + recent videos + pending collaborations.</summary>
        [HttpGet("dashboard")]
        [Authorize(Roles = "Creator")]
        public async Task<IActionResult> GetDashboard(CancellationToken ct)
        {
            var profile = await _registration.GetProfileAsync(GetUserId());
            if (profile == null)
                return NotFound(new { error = "Creator profile not found." });

            var channel      = await _channel.GetChannelAsync(profile.CreatorProfileId, ct);
            var recentVideos = channel != null
                ? await _channel.GetRecentVideosAsync(channel.ChannelId, 10, ct)
                : new List<ChannelVideoDto>();

            var pendingCount = await _db.CollaborationRequests
                .CountAsync(r => r.CreatorProfileId == profile.CreatorProfileId
                              && r.Status == "Pending", ct);

            var avgViews = recentVideos.Count > 0
                ? recentVideos.Average(v => (double)v.ViewCount)
                : 0.0;

            return Ok(new CreatorDashboardDto
            {
                Profile               = profile,
                Channel               = channel,
                RecentVideos          = recentVideos,
                PendingCollaborations = pendingCount,
                AvgViewsPerVideo      = avgViews
            });
        }

        /// <summary>
        /// Advanced creator insights: health scorecard, audience quality, and coaching recommendations.
        /// </summary>
        [HttpGet("insights")]
        [Authorize(Roles = "Creator")]
        public async Task<IActionResult> GetCreatorInsights(CancellationToken ct)
        {
            var insights = await _advancedAnalytics.GetCreatorSelfInsightsAsync(GetUserId(), ct);
            if (insights == null)
                return NotFound(new { error = "Creator profile not found." });
            return Ok(insights);
        }

        /// <summary>
        /// Creator-first onboarding and intelligence status:
        /// profile completeness, checklist, score explanations, weekly insight bullets.
        /// </summary>
        [HttpGet("onboarding-status")]
        [Authorize(Roles = "Creator")]
        public async Task<IActionResult> GetOnboardingStatus(CancellationToken ct)
        {
            try
            {
                var result = await _registration.GetOnboardingStatusAsync(GetUserId(), ct);
                return Ok(result);
            }
            catch (InvalidOperationException ex)
            {
                return NotFound(new { error = ex.Message });
            }
        }

        // ── Collaborations ────────────────────────────────────────────────────

        /// <summary>All collaboration requests sent to this creator.</summary>
        [HttpGet("collaborations")]
        [Authorize(Roles = "Creator")]
        public async Task<IActionResult> GetCollaborations()
        {
            var profileId = await GetCreatorProfileIdAsync();
            var results   = await _collaboration.GetCreatorRequestsAsync(profileId);
            return Ok(results);
        }

        /// <summary>Accept a pending collaboration request.</summary>
        [HttpPatch("collaborations/{id}/accept")]
        [Authorize(Roles = "Creator")]
        public async Task<IActionResult> AcceptCollaboration(int id)
        {
            try
            {
                var profileId = await GetCreatorProfileIdAsync();
                var result    = await _collaboration.AcceptRequestAsync(id, profileId);
                return Ok(result);
            }
            catch (InvalidOperationException ex)
            {
                return BadRequest(new { error = ex.Message });
            }
        }

        /// <summary>Reject a pending collaboration request.</summary>
        [HttpPatch("collaborations/{id}/reject")]
        [Authorize(Roles = "Creator")]
        public async Task<IActionResult> RejectCollaboration(int id)
        {
            try
            {
                var profileId = await GetCreatorProfileIdAsync();
                var result    = await _collaboration.RejectRequestAsync(id, profileId);
                return Ok(result);
            }
            catch (InvalidOperationException ex)
            {
                return BadRequest(new { error = ex.Message });
            }
        }

        // ── Helpers ───────────────────────────────────────────────────────────

        private int GetUserId() =>
            int.Parse(User.FindFirst(ClaimTypes.NameIdentifier)!.Value);

        private async Task<int> GetCreatorProfileIdAsync()
        {
            var userId  = GetUserId();
            var profile = await _db.CreatorProfiles
                .FirstOrDefaultAsync(p => p.UserId == userId);
            return profile?.CreatorProfileId
                ?? throw new InvalidOperationException("Creator profile not found.");
        }
    }
}
