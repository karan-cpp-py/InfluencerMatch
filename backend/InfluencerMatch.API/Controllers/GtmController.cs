using System;
using System.Security.Claims;
using System.Threading.Tasks;
using InfluencerMatch.Application.DTOs;
using InfluencerMatch.Application.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace InfluencerMatch.API.Controllers
{
    [ApiController]
    [Route("api/gtm")]
    public class GtmController : ControllerBase
    {
        private readonly IGtmService _gtm;

        public GtmController(IGtmService gtm)
        {
            _gtm = gtm;
        }

        [HttpPost("book-demo")]
        [AllowAnonymous]
        public async Task<IActionResult> BookDemo([FromBody] BookDemoLeadDto dto)
        {
            try
            {
                int? userId = null;
                var claim = User.FindFirstValue(ClaimTypes.NameIdentifier);
                if (int.TryParse(claim, out var parsed))
                {
                    userId = parsed;
                }

                var result = await _gtm.BookDemoAsync(userId, dto);
                return Ok(new { submitted = true, lead = result });
            }
            catch (InvalidOperationException ex)
            {
                return BadRequest(new { error = ex.Message });
            }
        }

        [HttpGet("referral")]
        [Authorize]
        public async Task<IActionResult> MyReferral()
        {
            var userId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
            var summary = await _gtm.GetOrCreateReferralCodeAsync(userId);
            var usage = await _gtm.GetReferralUsageAsync(userId);
            return Ok(new { summary, usage });
        }
    }
}
