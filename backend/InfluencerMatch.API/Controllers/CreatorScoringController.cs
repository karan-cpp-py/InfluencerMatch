using System.Threading.Tasks;
using InfluencerMatch.Application.DTOs;
using InfluencerMatch.Application.Interfaces;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;

namespace InfluencerMatch.API.Controllers
{
    /// <summary>
    /// Creator scoring and comparison.
    ///
    /// Endpoints:
    ///   GET  /api/creators/{creatorId}/score        – Get stored score + breakdown
    ///   POST /api/creators/{creatorId}/score/refresh – Recalculate score on-demand
    ///   GET  /api/creators/compare                   – Side-by-side comparison of two creators
    /// </summary>
    [ApiController]
    [Route("api/creators")]
    [Produces("application/json")]
    public class CreatorScoringController : ControllerBase
    {
        private readonly ICreatorScoringService _scoringService;
        private readonly ILogger<CreatorScoringController> _logger;

        public CreatorScoringController(
            ICreatorScoringService scoringService,
            ILogger<CreatorScoringController> logger)
        {
            _scoringService = scoringService;
            _logger         = logger;
        }

        /// <summary>
        /// Returns the latest calculated score for a creator, including component breakdown.
        ///
        /// Score formula (0 – 100):
        ///   0.4 × EngagementRate  + 0.3 × AverageViews
        /// + 0.2 × SubscriberGrowthRate + 0.1 × UploadFrequency
        /// </summary>
        [HttpGet("{creatorId:int}/score")]
        [ProducesResponseType(typeof(CreatorScoreDto), 200)]
        [ProducesResponseType(404)]
        public async Task<IActionResult> GetScore(int creatorId)
        {
            var result = await _scoringService.GetScoreAsync(creatorId);
            if (result == null)
                return NotFound(new { message = $"No score found for creator {creatorId}. Call /refresh to calculate." });
            return Ok(result);
        }

        /// <summary>
        /// Forces immediate score recalculation for a creator.
        /// </summary>
        [HttpPost("{creatorId:int}/score/refresh")]
        [ProducesResponseType(typeof(CreatorScoreDto), 200)]
        [ProducesResponseType(404)]
        public async Task<IActionResult> RefreshScore(int creatorId)
        {
            var result = await _scoringService.CalculateScoreAsync(creatorId);
            if (result == null)
                return NotFound(new { message = $"Creator {creatorId} not found." });
            return Ok(result);
        }

        /// <summary>
        /// Side-by-side comparison of two creators.
        ///
        /// Returns both creators' Subscribers, AverageViews, EngagementRate,
        /// UploadFrequency and CreatorScore in a single response.
        /// </summary>
        /// <param name="creatorId1">First creator's database ID</param>
        /// <param name="creatorId2">Second creator's database ID</param>
        [HttpGet("compare")]
        [ProducesResponseType(typeof(CreatorComparisonDto), 200)]
        [ProducesResponseType(400)]
        public async Task<IActionResult> Compare(
            [FromQuery] int creatorId1,
            [FromQuery] int creatorId2)
        {
            if (creatorId1 <= 0 || creatorId2 <= 0)
                return BadRequest(new { message = "Both creatorId1 and creatorId2 must be positive integers." });

            if (creatorId1 == creatorId2)
                return BadRequest(new { message = "creatorId1 and creatorId2 must be different." });

            var result = await _scoringService.CompareCreatorsAsync(creatorId1, creatorId2);
            return Ok(result);
        }
        /// <summary>
        /// Returns all creators ranked by their composite score (highest first).
        /// Optionally filter by category.
        /// </summary>
        /// <param name="page">Page number (1-based)</param>
        /// <param name="pageSize">Results per page (max 100)</param>
        /// <param name="category">Optional category filter</param>
        [HttpGet("leaderboard")]
        [ProducesResponseType(typeof(PagedResultDto<CreatorScoreDto>), 200)]
        public async Task<IActionResult> Leaderboard(
            [FromQuery] int page = 1,
            [FromQuery] int pageSize = 20,
            [FromQuery] string? category = null,
            [FromQuery] string? country = null)
        {
            if (pageSize > 100) pageSize = 100;
            var result = await _scoringService.GetLeaderboardAsync(page, pageSize, category, country);
            return Ok(result);
        }
    }
}
