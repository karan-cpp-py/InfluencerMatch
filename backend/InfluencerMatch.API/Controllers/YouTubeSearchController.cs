using System;
using System.Linq;
using System.Security.Claims;
using System.Text.RegularExpressions;
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
    [ApiController]
    [Route("api/youtube-search")]
    [Produces("application/json")]
    public class YouTubeSearchController : ControllerBase
    {
        private readonly IYouTubeSearchIntelligenceService _service;
        private readonly ApplicationDbContext              _db;

        public YouTubeSearchController(
            IYouTubeSearchIntelligenceService service,
            ApplicationDbContext db)
        {
            _service = service;
            _db      = db;
        }

        [HttpGet("search")]
        [Authorize(Roles = "Brand,Agency,Individual,CreatorManager,Creator")]
        [ProducesResponseType(typeof(YouTubeSearchResultDto), 200)]
        public async Task<IActionResult> Search(
            [FromQuery] string query,
            [FromQuery] string? category = null,
            [FromQuery] string? country = null,
            [FromQuery] string? language = null,
            [FromQuery] int limit = 20,
            CancellationToken ct = default)
        {
            if (string.IsNullOrWhiteSpace(query))
                return BadRequest(new { message = "query is required." });

            var response = await _service.SearchAsync(new YouTubeSearchQueryRequestDto
            {
                Query = query,
                Category = category,
                Country = country,
                Language = language,
                Limit = limit
            }, ct);

            return Ok(response);
        }

        /// <summary>
        /// Resolve a YouTube channel URL to a creator profile in the platform DB.
        /// Accepts: youtube.com/@handle, youtube.com/channel/UCxxx, youtube.com/c/name, youtu.be
        /// Falls back to a name-based search query if channel ID is not found locally.
        /// </summary>
        [HttpGet("resolve-channel")]
        [Authorize(Roles = "Brand,Agency,Individual,CreatorManager,Creator")]
        public async Task<IActionResult> ResolveChannel(
            [FromQuery] string url,
            CancellationToken ct = default)
        {
            if (string.IsNullOrWhiteSpace(url))
                return BadRequest(new { message = "url is required." });

            // Extract channel identifier from URL
            var channelId   = ExtractChannelId(url);      // UCxxx format
            var handleOrName = ExtractHandleOrName(url);   // @handle / name

            if (channelId != null)
            {
                var byId = await _db.Creators
                    .AsNoTracking()
                    .Where(c => c.ChannelId == channelId)
                    .Select(c => MapCreatorToResult(c))
                    .FirstOrDefaultAsync(ct);

                if (byId != null) return Ok(new { resolved = true, creator = byId });
            }

            if (handleOrName != null)
            {
                var clean = handleOrName.TrimStart('@').ToLowerInvariant();
                var byName = await _db.Creators
                    .AsNoTracking()
                    .Where(c => c.ChannelName.ToLower().Contains(clean) ||
                                (c.Description != null && c.Description.ToLower().Contains(clean)))
                    .OrderByDescending(c => c.Subscribers)
                    .Take(6)
                    .Select(c => MapCreatorToResult(c))
                    .ToListAsync(ct);

                if (byName.Any())
                    return Ok(new { resolved = true, creators = byName, searchQuery = handleOrName });
            }

            return Ok(new { resolved = false, message = "Channel not found in platform index. Try General Search." });
        }

        /// <summary>
        /// Resolve a YouTube video URL to the creator who uploaded it.
        /// Looks up ChannelVideos then joins to Creators table.
        /// </summary>
        [HttpGet("resolve-video")]
        [Authorize(Roles = "Brand,Agency,Individual,CreatorManager,Creator")]
        public async Task<IActionResult> ResolveVideo(
            [FromQuery] string url,
            CancellationToken ct = default)
        {
            if (string.IsNullOrWhiteSpace(url))
                return BadRequest(new { message = "url is required." });

            var videoId = ExtractVideoId(url);
            if (videoId == null)
                return BadRequest(new { message = "Could not extract a YouTube video ID from this URL. Make sure it's a valid youtube.com/watch?v=... or youtu.be/... link." });

            // Look up in ChannelVideos (self-registered creator videos)
            var channelVideo = await _db.ChannelVideos
                .AsNoTracking()
                .Where(v => v.YoutubeVideoId == videoId)
                .Select(v => new { v.YoutubeVideoId, v.ChannelId, v.Title })
                .FirstOrDefaultAsync(ct);

            if (channelVideo != null)
            {
                var creator = await _db.Creators
                    .AsNoTracking()
                    .Where(c => c.ChannelId == channelVideo.ChannelId)
                    .Select(c => MapCreatorToResult(c))
                    .FirstOrDefaultAsync(ct);

                if (creator != null)
                    return Ok(new
                    {
                        resolved = true,
                        videoId = channelVideo.YoutubeVideoId,
                        videoTitle = channelVideo.Title,
                        creator
                    });
            }

            // Fallback: VideoAnalytics table
            var va = await _db.VideoAnalytics
                .AsNoTracking()
                .Where(v => v.YoutubeVideoId == videoId)
                .Select(v => new { v.YoutubeVideoId, v.CreatorId, v.Title })
                .FirstOrDefaultAsync(ct);

            if (va != null)
            {
                var creator = await _db.Creators
                    .AsNoTracking()
                    .Where(c => c.CreatorId == va.CreatorId)
                    .Select(c => MapCreatorToResult(c))
                    .FirstOrDefaultAsync(ct);

                if (creator != null)
                    return Ok(new
                    {
                        resolved = true,
                        videoId = va.YoutubeVideoId,
                        videoTitle = va.Title,
                        creator
                    });
            }

            return Ok(new { resolved = false, message = "Video not found in platform index. The creator may not be indexed yet." });
        }

        [HttpPost("analyze")]
        [Authorize(Roles = "Brand,Agency,Individual,CreatorManager,Creator")]
        [ProducesResponseType(typeof(YouTubeCreatorAnalysisResponseDto), 200)]
        [ProducesResponseType(404)]
        public async Task<IActionResult> AnalyzeCreator(
            [FromBody] YouTubeCreatorAnalysisRequestDto request,
            CancellationToken ct = default)
        {
            if (request == null || (request.CreatorId <= 0 && string.IsNullOrWhiteSpace(request.ChannelId)))
                return BadRequest(new { message = "Either creatorId or channelId is required." });

            var result = await _service.AnalyzeCreatorAsync(request, ct);
            if (result == null)
                return NotFound(new { message = $"Creator {request.CreatorId} not found." });

            return Ok(result);
        }

        [HttpPost("shortlist/save")]
        [Authorize(Roles = "Brand,Agency,Individual,CreatorManager,Creator")]
        [ProducesResponseType(typeof(YouTubeShortlistSaveResponseDto), 200)]
        public async Task<IActionResult> SaveShortlist(
            [FromBody] YouTubeShortlistSaveRequestDto request,
            CancellationToken ct = default)
        {
            if (request == null)
                return BadRequest(new { message = "request body is required." });

            if (!int.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var userId) || userId <= 0)
                return Unauthorized(new { message = "Invalid user identity." });

            var result = await _service.SaveShortlistAsync(userId, request, ct);
            return Ok(result);
        }

        // ── URL parsing helpers ───────────────────────────────────────────────

        private static string? ExtractVideoId(string url)
        {
            // youtu.be/VIDEO_ID  or  ?v=VIDEO_ID
            var shortMatch = Regex.Match(url, @"youtu\.be/([A-Za-z0-9_-]{11})");
            if (shortMatch.Success) return shortMatch.Groups[1].Value;

            var longMatch = Regex.Match(url, @"[?&]v=([A-Za-z0-9_-]{11})");
            if (longMatch.Success) return longMatch.Groups[1].Value;

            return null;
        }

        private static string? ExtractChannelId(string url)
        {
            var m = Regex.Match(url, @"youtube\.com/channel/(UC[A-Za-z0-9_-]+)");
            return m.Success ? m.Groups[1].Value : null;
        }

        private static string? ExtractHandleOrName(string url)
        {
            // @handle
            var handle = Regex.Match(url, @"youtube\.com/@([A-Za-z0-9._-]+)");
            if (handle.Success) return "@" + handle.Groups[1].Value;

            // /c/name or /user/name
            var name = Regex.Match(url, @"youtube\.com/(?:c|user)/([A-Za-z0-9._-]+)");
            if (name.Success) return name.Groups[1].Value;

            return null;
        }

        private static object MapCreatorToResult(InfluencerMatch.Domain.Entities.Creator c) => new
        {
            creatorId       = c.CreatorId,
            channelId       = c.ChannelId,
            channelName     = c.ChannelName,
            category        = c.Category,
            country         = c.Country,
            language        = c.Language,
            subscribers     = c.Subscribers,
            totalViews      = c.TotalViews,
            videoCount      = c.VideoCount,
            engagementRate  = c.EngagementRate,
            thumbnailUrl    = c.ThumbnailUrl,
            channelUrl      = c.ChannelUrl ?? $"https://youtube.com/channel/{c.ChannelId}",
            creatorTier     = c.CreatorTier,
            relevanceScore  = 1.0,
            relevanceReason = "Exact match from URL"
        };
    }
}
