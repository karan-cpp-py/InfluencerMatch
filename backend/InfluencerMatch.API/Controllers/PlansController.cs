using System;
using System.Threading.Tasks;
using InfluencerMatch.Application.Interfaces;
using Microsoft.AspNetCore.Mvc;

namespace InfluencerMatch.API.Controllers
{
    [ApiController]
    [Route("api/plans")]
    public class PlansController : ControllerBase
    {
        private readonly ISubscriptionPlanService _planService;

        public PlansController(ISubscriptionPlanService planService)
        {
            _planService = planService;
        }

        [HttpGet]
        public async Task<IActionResult> GetPlans()
        {
            var plans = await _planService.GetPlansAsync();
            return Ok(plans);
        }
    }
}
