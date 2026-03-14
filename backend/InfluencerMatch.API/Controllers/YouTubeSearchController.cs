using System;
using System.Security.Claims;
using System.Threading;
using System.Threading.Tasks;
using InfluencerMatch.Application.DTOs;
using InfluencerMatch.Application.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace InfluencerMatch.API.Controllers
{
    [ApiController]
    [Route("api/youtube-search")]
    [Produces("application/json")]
    public class YouTubeSearchController : ControllerBase
    {
        private readonly IYouTubeSearchIntelligenceService _service;

        public YouTubeSearchController(IYouTubeSearchIntelligenceService service)
        {
            _service = service;
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
    }
}
