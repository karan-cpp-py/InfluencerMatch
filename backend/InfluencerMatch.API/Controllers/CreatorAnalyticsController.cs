using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using InfluencerMatch.Application.DTOs;
using InfluencerMatch.Application.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;

namespace InfluencerMatch.API.Controllers
{
    /// <summary>
    /// Creator analytics — per-creator analytics profile, search with filters,
    /// and language-based discovery.
    /// </summary>
    [ApiController]
    [Route("api/creators")]
    [Produces("application/json")]
    public class CreatorAnalyticsController : ControllerBase
    {
        private readonly ICreatorAnalyticsService   _analyticsService;
        private readonly ILanguageDetectionService  _langService;
        private readonly IVideoAnalyticsService     _videoAnalytics;
        private readonly ILogger<CreatorAnalyticsController> _logger;

        public CreatorAnalyticsController(
            ICreatorAnalyticsService  analyticsService,
            ILanguageDetectionService langService,
            IVideoAnalyticsService    videoAnalytics,
            ILogger<CreatorAnalyticsController> logger)
        {
            _analyticsService = analyticsService;
            _langService      = langService;
            _videoAnalytics   = videoAnalytics;
            _logger           = logger;
        }

        /// <summary>
        /// Returns the full analytics profile for a single creator:
        /// ChannelName, Subscribers, AverageViews, EngagementRate,
        /// TotalVideos, GrowthHistory and TopVideos.
        /// </summary>
        /// <param name="creatorId">Database ID of the creator</param>
        [HttpGet("{creatorId:int}/analytics")]
        [ProducesResponseType(typeof(CreatorAnalyticsDto), 200)]
        [ProducesResponseType(404)]
        public async Task<IActionResult> GetAnalytics(int creatorId)
        {
            var result = await _analyticsService.GetCreatorAnalyticsAsync(creatorId);
            if (result == null)
            {
                _logger.LogWarning("Analytics requested for unknown creatorId={Id}", creatorId);
                return NotFound(new { message = $"Creator {creatorId} not found." });
            }
            return Ok(result);
        }

        /// <summary>
        /// Triggers an on-demand analytics recalculation for a single creator.
        /// </summary>
        [HttpPost("{creatorId:int}/analytics/refresh")]
        [ProducesResponseType(200)]
        [ProducesResponseType(404)]
        public async Task<IActionResult> RefreshAnalytics(int creatorId)
        {
            var ok = await _analyticsService.RefreshAnalyticsAsync(creatorId);
            if (!ok) return NotFound(new { message = $"Creator {creatorId} not found." });
            return Ok(new { message = "Analytics refreshed." });
        }

        /// <summary>
        /// Search and filter creators.
        ///
        /// Query params:
        ///   category, platform, minSubscribers, maxSubscribers,
        ///   minEngagement, maxEngagement, search, language, region,
        ///   sortBy (subscribers|views|engagement|videos|newest),
        ///   page, pageSize
        /// </summary>
        [HttpGet("search")]
        [Authorize(Roles = "Brand,Agency,Individual,CreatorManager")]
        [ProducesResponseType(typeof(PagedResultDto<CreatorSearchResultDto>), 200)]
        public async Task<IActionResult> Search([FromQuery] CreatorSearchQueryDto query)
        {
            if (query.PageSize > 100) query.PageSize = 100; // cap page size
            var result = await _analyticsService.SearchCreatorsAsync(query);
            return Ok(result);
        }

        // ── Feature 6: Language Detection ─────────────────────────────────────

        /// <summary>
        /// Returns the list of supported languages for the language filter dropdown.
        /// </summary>
        [HttpGet("languages")]
        [ProducesResponseType(typeof(IReadOnlyList<string>), 200)]
        public IActionResult GetSupportedLanguages()
            => Ok(_langService.GetSupportedLanguages());

        /// <summary>
        /// Triggers an immediate language-detection refresh for all creators.
        /// </summary>
        [HttpPost("languages/refresh")]
        [ProducesResponseType(200)]
        public async Task<IActionResult> RefreshLanguages(CancellationToken ct)
        {
            await _langService.RefreshAllAsync(ct);
            return Ok(new { message = "Language detection refresh triggered." });
        }

        // ── Feature 8: Video Analytics ────────────────────────────────────────

        /// <summary>
        /// Returns a per-creator video analytics summary: organic vs sponsored breakdown,
        /// avg views, avg engagement rate, detected brands, and per-video rows.
        /// Data is read from the pre-computed <c>VideoAnalytics</c> table — run the
        /// SuperAdmin job "video-analytics" first to populate it.
        /// </summary>
        [HttpGet("{creatorId:int}/video-analytics")]
        [ProducesResponseType(typeof(CreatorVideoAnalyticsSummaryDto), 200)]
        [ProducesResponseType(404)]
        public async Task<IActionResult> GetVideoAnalytics(int creatorId)
        {
            var result = await _videoAnalytics.GetCreatorSummaryAsync(creatorId);
            if (result == null)
                return NotFound(new { message = $"Creator {creatorId} not found." });
            return Ok(result);
        }

        /// <summary>
        /// Triggers an on-demand video-analytics refresh for a single creator.
        /// Fetches the 50 most-recent videos from YouTube, computes engagement,
        /// detects brand collaborations and upserts into <c>VideoAnalytics</c>.
        /// </summary>
        [HttpPost("{creatorId:int}/video-analytics/refresh")]
        [ProducesResponseType(200)]
        [ProducesResponseType(404)]
        public async Task<IActionResult> RefreshVideoAnalytics(int creatorId, CancellationToken ct)
        {
            var n = await _videoAnalytics.RefreshCreatorAsync(creatorId, ct);
            if (n < 0)
                return NotFound(new { message = $"Creator {creatorId} not found." });
            return Ok(new { message = $"Video analytics refreshed. {n} rows upserted." });
        }
    }
}
