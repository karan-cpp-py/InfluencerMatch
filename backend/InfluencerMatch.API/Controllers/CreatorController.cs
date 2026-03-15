using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using System.Threading;
using System.Threading.Tasks;
using InfluencerMatch.Application.DTOs;
using InfluencerMatch.Application.Interfaces;
using InfluencerMatch.Domain.Entities;
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
        private readonly IGroqLlmService             _groq;
        private readonly ApplicationDbContext        _db;

        public CreatorController(
            ICreatorRegistrationService registration,
            ICreatorChannelService      channel,
            ICollaborationService       collaboration,
            IAdvancedAnalyticsService   advancedAnalytics,
            IGroqLlmService             groq,
            ApplicationDbContext        db)
        {
            _registration      = registration;
            _channel           = channel;
            _collaboration     = collaboration;
            _advancedAnalytics = advancedAnalytics;
            _groq              = groq;
            _db                = db;
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
        public async Task<IActionResult> GetProfile(CancellationToken ct)
        {
            await EnsureCreatorProfileForCurrentUserAsync(ct);
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

        [HttpPost("audience-demographics/ingest")]
        [Authorize(Roles = "Creator")]
        public async Task<IActionResult> IngestAudienceDemographics(
            [FromBody] AudienceDemographicsIngestRequestDto dto,
            CancellationToken ct)
        {
            try
            {
                var profileId = await GetCreatorProfileIdAsync();
                var result = await _channel.IngestAudienceDemographicsAsync(
                    profileId,
                    dto.AccessToken,
                    dto.StartDate,
                    dto.EndDate,
                    ct);
                return Ok(result);
            }
            catch (InvalidOperationException ex)
            {
                return BadRequest(new { error = ex.Message });
            }
        }

        [HttpPost("audience-demographics/connect-url")]
        [Authorize(Roles = "Creator")]
        public async Task<IActionResult> GetAudienceDemographicsConnectUrl(
            [FromBody] CreatorYouTubeAnalyticsConnectRequestDto dto,
            CancellationToken ct)
        {
            try
            {
                var profileId = await GetCreatorProfileIdAsync();
                var result = await _channel.GetYouTubeAnalyticsConnectUrlAsync(profileId, dto.RedirectUri, ct);
                return Ok(result);
            }
            catch (InvalidOperationException ex)
            {
                return BadRequest(new { error = ex.Message });
            }
        }

        [HttpPost("audience-demographics/exchange-code")]
        [Authorize(Roles = "Creator")]
        public async Task<IActionResult> ExchangeAudienceDemographicsCode(
            [FromBody] CreatorYouTubeAnalyticsConnectCodeDto dto,
            CancellationToken ct)
        {
            try
            {
                var profileId = await GetCreatorProfileIdAsync();
                await _channel.ExchangeYouTubeAnalyticsCodeAsync(profileId, dto.RedirectUri, dto.Code, ct);
                return Ok(new { message = "YouTube Analytics connected." });
            }
            catch (InvalidOperationException ex)
            {
                return BadRequest(new { error = ex.Message });
            }
        }

        [HttpGet("audience-demographics")]
        [Authorize(Roles = "Creator")]
        public async Task<IActionResult> GetAudienceDemographics(CancellationToken ct)
        {
            var profileId = await GetCreatorProfileIdAsync();
            var result = await _channel.GetAudienceDemographicsAsync(profileId, ct);
            if (result == null)
                return NotFound(new { error = "No audience demographics snapshot available. Ingest first." });

            return Ok(result);
        }

        // ── Dashboard ────────────────────────────────────────────────────────

        /// <summary>Creator dashboard: profile + channel stats + recent videos + pending collaborations.</summary>
        [HttpGet("dashboard")]
        [Authorize(Roles = "Creator")]
        public async Task<IActionResult> GetDashboard(CancellationToken ct)
        {
            await EnsureCreatorProfileForCurrentUserAsync(ct);
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
            await EnsureCreatorProfileForCurrentUserAsync(ct);
            var insights = await _advancedAnalytics.GetCreatorSelfInsightsAsync(GetUserId(), ct);
            if (insights == null)
                return NotFound(new { error = "Creator profile not found." });
            return Ok(insights);
        }

        /// <summary>
        /// AI-powered ranked analysis of the creator's recent 10 videos.
        /// Scores each video using engagement metrics + view velocity, calls Groq LLM for
        /// per-video narrative, and returns topic recommendations based on the top 3.
        /// </summary>
        [HttpGet("videos/ranked-insights")]
        [Authorize(Roles = "Creator")]
        public async Task<IActionResult> GetRankedVideoInsights(CancellationToken ct)
        {
            var profile = await _registration.GetProfileAsync(GetUserId());
            if (profile == null) return NotFound(new { error = "Creator profile not found." });

            var channel = await _channel.GetChannelAsync(profile.CreatorProfileId, ct);
            if (channel == null) return NotFound(new { error = "No channel linked yet." });

            var videos = await _channel.GetRecentVideosAsync(channel.ChannelId, 10, ct);
            if (videos == null || !videos.Any())
                return Ok(new { channelName = channel.ChannelName, avgViews = 0L, ranked = Array.Empty<object>(), topicAdvice = Array.Empty<string>() });

            var now       = DateTime.UtcNow;
            double avgViews = videos.Average(v => (double)v.ViewCount);
            if (avgViews <= 0) avgViews = 1;

            // Max view-velocity across all videos (for normalisation)
            double maxVelocity = videos
                .Select(v => v.ViewCount / Math.Max((now - v.PublishedAt).TotalDays, 1))
                .DefaultIfEmpty(1)
                .Max();
            if (maxVelocity <= 0) maxVelocity = 1;

            // Compute composite score: 40% relative views + 35% engagement + 25% view velocity
            var scored = videos.Select(v =>
            {
                double engRate     = v.ViewCount > 0 ? (double)(v.LikeCount + v.CommentCount) / v.ViewCount : 0.0;
                double daysOld     = Math.Max((now - v.PublishedAt).TotalDays, 1);
                double velocity    = v.ViewCount / daysOld;
                double normViews   = v.ViewCount / avgViews;
                double normEng     = Math.Min(engRate / 0.06, 1.0);     // 6 % engagement = full score
                double normVel     = velocity / maxVelocity;
                double composite   = 0.40 * normViews + 0.35 * normEng + 0.25 * normVel;
                return (Video: v, EngRate: engRate, Velocity: velocity, Composite: composite);
            })
            .OrderByDescending(x => x.Composite)
            .Select((x, i) => (Rank: i + 1, x.Video, x.EngRate, x.Velocity, x.Composite))
            .ToList();

            // LLM narratives — run in parallel (bounded by Groq free-tier: 30 RPM)
            var narrativeTasks = scored.Select(async s =>
            {
                var daysAgo     = (int)Math.Round((now - s.Video.PublishedAt).TotalDays);
                var perfLabel   = s.Rank <= 3 ? "a top performer (#" + s.Rank + " out of 10)"
                                : s.Rank >= 8  ? "below average (#" + s.Rank + " out of 10)"
                                              : "average (#" + s.Rank + " out of 10)";
                var userPrompt  = $"Video: \"{s.Video.Title}\" | Views: {s.Video.ViewCount:N0} | " +
                                  $"Likes: {s.Video.LikeCount:N0} | Comments: {s.Video.CommentCount:N0} | " +
                                  $"Published {daysAgo} days ago | Channel avg views: {avgViews:N0} | " +
                                  $"Engagement rate: {s.EngRate * 100:F2}% | Rank: {perfLabel}. " +
                                  "In 2-3 concise sentences explain WHY this video is performing at this level " +
                                  "and what specific element (title hook, topic, format, or posting timing) is most responsible.";
                return await _groq.GenerateTextAsync(
                    "You are an expert YouTube content analyst. Give direct, specific, actionable feedback about video performance. Avoid generic tips.",
                    userPrompt, 150);
            }).ToList();

            await Task.WhenAll(narrativeTasks);
            var narratives = narrativeTasks.Select(t => t.Result).ToList();

            // Topic recommendations from top-3 video titles
            var top3Titles   = scored.Take(3).Select(s => $"\"{s.Video.Title}\"").ToList();
            var topicPrompt  = $"A YouTube creator's top 3 performing videos are: {string.Join(", ", top3Titles)}. " +
                               $"Channel category: General. " +
                               "List exactly 3 specific video topic recommendations for their next uploads. " +
                               "Format: numbered list. Each item = compelling title idea (15 words max) + 1-sentence reason (why it will perform well).";
            var topicRaw     = await _groq.GenerateTextAsync(
                "You are a YouTube growth strategist helping creators plan high-performing content.",
                topicPrompt, 250);

            var topicLines = (topicRaw ?? string.Empty)
                .Split('\n', StringSplitOptions.RemoveEmptyEntries)
                .Select(l => l.Trim())
                .Where(l => !string.IsNullOrWhiteSpace(l))
                .Take(9)
                .ToList();

            return Ok(new
            {
                channelName = channel.ChannelName,
                avgViews = (long)avgViews,
                ranked = scored.Select((s, idx) => new
                {
                    rank           = s.Rank,
                    youtubeVideoId = s.Video.YoutubeVideoId,
                    title          = s.Video.Title,
                    thumbnailUrl   = s.Video.ThumbnailUrl,
                    viewCount      = s.Video.ViewCount,
                    likeCount      = s.Video.LikeCount,
                    commentCount   = s.Video.CommentCount,
                    publishedAt    = s.Video.PublishedAt,
                    daysAgo        = (int)Math.Round((now - s.Video.PublishedAt).TotalDays),
                    engagementRate = Math.Round(s.EngRate * 100, 2),
                    compositeScore = Math.Round(s.Composite * 100, 1),
                    performanceLabel = s.Rank <= 3 ? "Top Performer"
                                    : s.Rank >= 8  ? "Needs Attention"
                                                   : "Average",
                    narrative = narratives[idx] ?? "Analysis unavailable — ensure Groq:ApiKey is configured."
                }).ToList(),
                topicAdvice = topicLines
            });
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
                await EnsureCreatorProfileForCurrentUserAsync(ct);
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
            var profile = await EnsureCreatorProfileForCurrentUserAsync();
            return profile?.CreatorProfileId
                ?? throw new InvalidOperationException("Creator profile not found.");
        }

        private async Task<CreatorProfile?> EnsureCreatorProfileForCurrentUserAsync(CancellationToken ct = default)
        {
            var userId = GetUserId();
            var existing = await _db.CreatorProfiles
                .FirstOrDefaultAsync(p => p.UserId == userId, ct);
            if (existing != null)
            {
                return existing;
            }

            var user = await _db.Users
                .AsNoTracking()
                .FirstOrDefaultAsync(u => u.UserId == userId, ct);
            if (user == null || !string.Equals(user.Role, "Creator", StringComparison.OrdinalIgnoreCase))
            {
                return null;
            }

            var bootstrapProfile = new CreatorProfile
            {
                UserId = userId,
                Country = string.IsNullOrWhiteSpace(user.Country) ? "Unknown" : user.Country,
                ContactEmail = user.Email,
                CreatedAt = DateTime.UtcNow
            };

            _db.CreatorProfiles.Add(bootstrapProfile);
            await _db.SaveChangesAsync(ct);
            return bootstrapProfile;
        }
    }
}
