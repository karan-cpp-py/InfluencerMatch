using System.Threading.Tasks;
using InfluencerMatch.Application.DTOs;
using InfluencerMatch.Application.Interfaces;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;

namespace InfluencerMatch.API.Controllers
{
    /// <summary>
    /// Brand-level analytics: who is promoting a brand and what is the estimated reach.
    ///
    /// Endpoints:
    ///   GET /api/brands/{brandName}/analytics   – Full campaign analytics for a brand
    ///   GET /api/brands/{brandName}/mentions     – Raw list of individual video detections
    /// </summary>
    [ApiController]
    [Route("api/brands")]
    [Produces("application/json")]
    public class BrandAnalyticsController : ControllerBase
    {
        private readonly IBrandPromotionService _brandService;
        private readonly IVideoAnalyticsService _videoAnalytics;
        private readonly ILogger<BrandAnalyticsController> _logger;

        public BrandAnalyticsController(
            IBrandPromotionService brandService,
            IVideoAnalyticsService videoAnalytics,
            ILogger<BrandAnalyticsController> logger)
        {
            _brandService   = brandService;
            _videoAnalytics = videoAnalytics;
            _logger         = logger;
        }

        /// <summary>
        /// Aggregated campaign analytics for a brand.
        ///
        /// Returns:
        ///   • How many creators are promoting the brand
        ///   • Total videos detected
        ///   • Estimated total views and engagement generated
        ///   • Per-creator breakdown
        /// </summary>
        /// <param name="brandName">
        /// Brand name or hashtag (e.g.  "samsung",  "#ad",  "@nike").
        /// The search is case-insensitive and performs a partial match.
        /// </param>
        [HttpGet("{brandName}/analytics")]
        [ProducesResponseType(typeof(BrandAnalyticsDto), 200)]
        [ProducesResponseType(404)]
        public async Task<IActionResult> GetBrandAnalytics(string brandName)
        {
            if (string.IsNullOrWhiteSpace(brandName))
                return BadRequest(new { message = "brandName is required." });

            var result = await _brandService.GetBrandAnalyticsAsync(brandName);
            if (result == null)
                return NotFound(new { message = $"No promotions detected for brand '{brandName}'." });

            return Ok(result);
        }

        /// <summary>
        /// Raw list of individual brand-mention detections for a brand.
        /// Useful for auditing and manual review.
        /// </summary>
        [HttpGet("{brandName}/mentions")]
        [ProducesResponseType(typeof(System.Collections.Generic.List<BrandMentionDto>), 200)]
        public async Task<IActionResult> GetBrandMentions(string brandName)
        {
            if (string.IsNullOrWhiteSpace(brandName))
                return BadRequest(new { message = "brandName is required." });

            var result = await _brandService.GetMentionsForBrandAsync(brandName);
            return Ok(result);
        }

        /// <summary>
        /// Trigger an on-demand brand-promotion scan for all creators.
        /// </summary>
        [HttpPost("scan")]
        [ProducesResponseType(200)]
        public async Task<IActionResult> TriggerScan()
        {
            _ = Task.Run(() => _brandService.ScanAllCreatorsAsync());
            return Ok(new { message = "Brand-promotion scan started in the background." });
        }

        // ── Feature 8: Video Analytics brand endpoints ────────────────────────

        /// <summary>
        /// Returns all creators who promoted <paramref name="brandName"/> based on
        /// the pre-computed <c>VideoAnalytics</c> table, with per-creator video
        /// counts, total views and average engagement.
        ///
        /// Run the SuperAdmin "video-analytics" job first to populate the table.
        /// </summary>
        [HttpGet("{brandName}/creators")]
        [ProducesResponseType(typeof(BrandCreatorStatsDto), 200)]
        public async Task<IActionResult> GetBrandCreators(string brandName)
        {
            if (string.IsNullOrWhiteSpace(brandName))
                return BadRequest(new { message = "brandName is required." });

            var result = await _videoAnalytics.GetBrandCreatorsAsync(brandName);
            return Ok(result);
        }
    }
}
