using System.Threading.Tasks;
using InfluencerMatch.Application.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace InfluencerMatch.API.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    [Authorize]
    public class MatchController : ControllerBase
    {
        private readonly IMatchService _service;

        public MatchController(IMatchService service)
        {
            _service = service;
        }

        [HttpGet("campaign/{campaignId}")]
        public async Task<IActionResult> GetMatches(int campaignId, [FromQuery] bool includeOverBudget = false)
        {
            var results = await _service.MatchCampaignAsync(campaignId, includeOverBudget);
            return Ok(results);
        }
    }
}