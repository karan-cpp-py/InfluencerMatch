using System;
using System.Linq;
using System.Threading.Tasks;
using System.Security.Claims;
using InfluencerMatch.Application.DTOs;
using InfluencerMatch.Infrastructure.Data;
using InfluencerMatch.Domain.Entities;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace InfluencerMatch.API.Controllers
{
    [ApiController]
    [Route("api/funnel")]
    public class FunnelController : ControllerBase
    {
        private readonly ApplicationDbContext _db;

        public FunnelController(ApplicationDbContext db)
        {
            _db = db;
        }

        [HttpPost("events")]
        [AllowAnonymous]
        public async Task<IActionResult> Track([FromBody] TrackFunnelEventDto dto)
        {
            if (string.IsNullOrWhiteSpace(dto.EventName))
            {
                return BadRequest(new { error = "Event name is required." });
            }

            int? userId = null;
            var userClaim = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (int.TryParse(userClaim, out var parsed))
            {
                userId = parsed;
            }

            var role = User.FindFirstValue(ClaimTypes.Role);

            var normalizedEventName = dto.EventName.Trim();
            var dedupeEvents = new[]
            {
                "signup",
                "profile_completion",
                "first_search",
                "first_request_sent",
                "subscription_conversion"
            };

            if (userId.HasValue && dedupeEvents.Contains(normalizedEventName))
            {
                var alreadyTracked = await _db.FunnelEvents
                    .AsNoTracking()
                    .AnyAsync(x => x.UserId == userId.Value && x.EventName == normalizedEventName);
                if (alreadyTracked)
                {
                    return Ok(new { tracked = true, duplicate = true });
                }
            }

            _db.FunnelEvents.Add(new FunnelEvent
            {
                UserId = userId,
                Role = role,
                EventName = normalizedEventName,
                MetadataJson = dto.MetadataJson,
                CreatedAt = DateTime.UtcNow
            });

            await _db.SaveChangesAsync();
            return Ok(new { tracked = true });
        }

        [HttpGet("summary")]
        [Authorize(Roles = "SuperAdmin")]
        public async Task<IActionResult> Summary([FromQuery] int days = 30)
        {
            days = Math.Clamp(days, 1, 180);
            var since = DateTime.UtcNow.AddDays(-days);

            var rows = await _db.FunnelEvents
                .AsNoTracking()
                .Where(x => x.CreatedAt >= since)
                .GroupBy(x => x.EventName)
                .Select(g => new { eventName = g.Key, count = g.Count() })
                .OrderByDescending(x => x.count)
                .ToListAsync();

            return Ok(new { since, rows });
        }
    }
}
