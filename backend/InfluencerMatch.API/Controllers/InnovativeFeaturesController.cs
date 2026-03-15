using System.Threading.Tasks;
using InfluencerMatch.Application.DTOs;
using InfluencerMatch.Application.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace InfluencerMatch.API.Controllers
{
    [ApiController]
    [Route("api")]
    public class InnovativeFeaturesController : ControllerBase
    {
        private readonly IRisingCreatorService      _risingService;
        private readonly IBrandOpportunityService   _opportunityService;
        private readonly ICampaignPredictionService _predictionService;
        private readonly ICreatorPricingService     _pricingService;
        private readonly IViralContentService       _viralService;
        private readonly IYouTubeVideoAnalysisService _videoAnalysisService;
        private readonly IYouTubeCreatorImportService _youTubeImportService;

        public InnovativeFeaturesController(
            IRisingCreatorService      risingService,
            IBrandOpportunityService   opportunityService,
            ICampaignPredictionService predictionService,
            ICreatorPricingService     pricingService,
            IViralContentService       viralService,
            IYouTubeVideoAnalysisService videoAnalysisService,
            IYouTubeCreatorImportService youTubeImportService)
        {
            _risingService      = risingService;
            _opportunityService = opportunityService;
            _predictionService  = predictionService;
            _pricingService     = pricingService;
            _viralService       = viralService;
            _videoAnalysisService = videoAnalysisService;
            _youTubeImportService = youTubeImportService;
        }

        // ── Feature 1: Rising Creator Detection ──────────────────────────────
        // GET /api/creators/rising?topN=50&growthCategory=Rising&country=IN

        [HttpGet("creators/rising")]
        public async Task<IActionResult> GetRisingCreators(
            [FromQuery] int    topN           = 50,
            [FromQuery] string? growthCategory = null,
            [FromQuery] string? country       = null)
        {
            var results = await _risingService.GetRisingCreatorsAsync(topN, growthCategory, country);
            return Ok(results);
        }

        // ── Feature 2: Brand Opportunity Finder ──────────────────────────────
        // POST /api/brands/opportunities

        [HttpPost("brands/opportunities")]
        public async Task<IActionResult> FindBrandOpportunities([FromBody] BrandOpportunityRequestDto request)
        {
            var results = await _opportunityService.FindOpportunitiesAsync(request);
            return Ok(results);
        }

        // ── Feature 3: Campaign Performance Prediction ───────────────────────
        // GET /api/creators/{id}/campaign-prediction

        [HttpGet("creators/{id:int}/campaign-prediction")]
        public async Task<IActionResult> GetCampaignPrediction(int id)
        {
            var result = await _predictionService.PredictAsync(id);
            if (result == null) return NotFound(new { message = $"No analytics data found for creator {id}." });
            return Ok(result);
        }

        // ── Feature 4: Creator Price Estimation ──────────────────────────────
        // GET /api/creators/{id}/estimated-price

        [HttpGet("creators/{id:int}/estimated-price")]
        public async Task<IActionResult> GetEstimatedPrice(int id)
        {
            var result = await _pricingService.EstimatePriceAsync(id);
            if (result == null) return NotFound(new { message = $"No analytics data found for creator {id}." });
            return Ok(result);
        }

        // ── Trigger: force-recalculate growth scores ─────────────────────────
        // POST /api/creators/rising/recalculate

        [HttpPost("creators/rising/recalculate")]
        public async Task<IActionResult> RecalculateGrowthScores()
        {
            await _risingService.RecalculateAllGrowthScoresAsync();
            return Ok(new { message = "Growth score recalculation triggered successfully." });
        }

        // ── Feature 5: Viral Content Prediction ──────────────────────────────
        // GET /api/videos/trending?topN=50&category=Gaming&country=US

        [HttpGet("videos/trending")]
        [Authorize(Roles = "Creator")]
        public async Task<IActionResult> GetTrendingVideos(
            [FromQuery] int     topN     = 50,
            [FromQuery] string? category = null,
            [FromQuery] string? country  = null)
        {
            var results = await _viralService.GetTrendingVideosAsync(topN, category, country);
            return Ok(results);
        }

        // POST /api/videos/trending/refresh  — manual trigger
        [HttpPost("videos/trending/refresh")]
        public async Task<IActionResult> RefreshViralScores()
        {
            await _viralService.RefreshViralScoresAsync();
            return Ok(new { message = "Viral score refresh triggered successfully." });
        }

        // ── Feature: Single latest video AI analysis ───────────────────────
        // POST /api/videos/latest-analysis

        [HttpPost("videos/latest-analysis")]
        public async Task<IActionResult> AnalyzeLatestVideo([FromBody] YouTubeVideoAnalysisRequestDto request)
        {
            if (request?.Video == null)
            {
                return BadRequest(new { error = "Video payload is required." });
            }

            var result = await _videoAnalysisService.AnalyzeLatestVideoAsync(request);
            return Ok(result);
        }

        // ── Feature: Creator Intelligence YouTube Preview (non-persistent) ───
        // POST /api/brands/creator-intelligence/youtube-preview

        [HttpPost("brands/creator-intelligence/youtube-preview")]
        [Authorize(Roles = "Brand,Agency,Individual,CreatorManager,SuperAdmin")]
        public async Task<IActionResult> PreviewYouTubeCreators([FromBody] YouTubeImportRequestDto request)
        {
            if (request == null || string.IsNullOrWhiteSpace(request.Query))
                return BadRequest(new { error = "Query is required." });

            try
            {
                request.PersistResults = false;
                request.IncludeAiInsights = true;
                request.MaxResults = request.MaxResults <= 0 ? 12 : request.MaxResults;
                request.MaxVideosPerChannel = request.MaxVideosPerChannel <= 0 ? 8 : request.MaxVideosPerChannel;

                var result = await _youTubeImportService.ImportAsync(request);
                return Ok(result);
            }
            catch (System.InvalidOperationException ex)
            {
                return BadRequest(new { error = ex.Message });
            }
        }
    }
}
