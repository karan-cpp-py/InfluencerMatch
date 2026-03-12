using System.Threading.Tasks;
using System.Security.Claims;
using InfluencerMatch.Application.DTOs;
using InfluencerMatch.Application.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace InfluencerMatch.API.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    [Authorize(Roles = "Brand,Agency")]
    public class CampaignController : ControllerBase
    {
        private readonly ICampaignService _service;
        private readonly IAdvancedAnalyticsService _advancedAnalytics;

        public CampaignController(
            ICampaignService service,
            IAdvancedAnalyticsService advancedAnalytics)
        {
            _service = service;
            _advancedAnalytics = advancedAnalytics;
        }

        [HttpPost]
        public async Task<IActionResult> Create(CampaignDto dto)
        {
            dto.BrandId = GetUserId();
            var result = await _service.CreateAsync(dto);
            return Ok(result);
        }

        [HttpPut("{id}")]
        public async Task<IActionResult> Update(int id, CampaignDto dto)
        {
            var existing = await _service.GetByIdAsync(id);
            if (existing == null) return NotFound();

            if (existing.BrandId != GetUserId())
            {
                return Forbid();
            }

            dto.BrandId = existing.BrandId;
            var result = await _service.UpdateAsync(id, dto);
            if (result == null) return NotFound();
            return Ok(result);
        }

        [AllowAnonymous]
        [HttpGet("{id}")]
        public async Task<IActionResult> Get(int id)
        {
            var result = await _service.GetByIdAsync(id);
            if (result == null) return NotFound();
            return Ok(result);
        }

        [AllowAnonymous]
        [HttpGet("brand/{brandId}")]
        public async Task<IActionResult> GetByBrand(int brandId)
        {
            var list = await _service.GetByBrandIdAsync(brandId);
            return Ok(list);
        }

        [AllowAnonymous]
        [HttpGet("all")]
        public async Task<IActionResult> GetAll()
        {
            var list = await _service.GetAllAsync();
            return Ok(list);
        }

        [HttpGet("{id}/outcomes")]
        public async Task<IActionResult> GetOutcomeAnalytics(int id)
        {
            var existing = await _service.GetByIdAsync(id);
            if (existing == null) return NotFound();

            if (existing.BrandId != GetUserId())
                return Forbid();

            var result = await _advancedAnalytics.GetCampaignOutcomeAnalyticsAsync(id);
            if (result == null) return NotFound();
            return Ok(result);
        }

        [HttpPost("{id}/forecast")]
        public async Task<IActionResult> GetForecast(int id, [FromBody] CampaignForecastRequestDto dto)
        {
            var existing = await _service.GetByIdAsync(id);
            if (existing == null) return NotFound();

            if (existing.BrandId != GetUserId())
                return Forbid();

            var result = await _advancedAnalytics.GetPreCampaignForecastAsync(id, dto?.BudgetOverride);
            if (result == null) return NotFound();
            return Ok(result);
        }

        private int GetUserId()
            => int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
    }

    public class CampaignForecastRequestDto
    {
        public decimal? BudgetOverride { get; set; }
    }
}