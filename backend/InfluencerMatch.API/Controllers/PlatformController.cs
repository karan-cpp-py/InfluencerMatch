using System.Threading.Tasks;
using System.Security.Claims;
using InfluencerMatch.Application.DTOs;
using InfluencerMatch.API.Configuration;
using InfluencerMatch.Domain.Entities;
using InfluencerMatch.Infrastructure.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace InfluencerMatch.API.Controllers
{
    [ApiController]
    [Route("api/platform")]
    public class PlatformController : ControllerBase
    {
        private readonly PlatformStrategyOptions _options;
        private readonly ApplicationDbContext _db;
        private readonly ILogger<PlatformController> _logger;

        public PlatformController(
            IOptions<PlatformStrategyOptions> options,
            ApplicationDbContext db,
            ILogger<PlatformController> logger)
        {
            _options = options.Value;
            _db = db;
            _logger = logger;
        }

        [HttpGet("config")]
        [AllowAnonymous]
        public async Task<IActionResult> GetConfig()
        {
            var creatorCount = await _db.CreatorProfiles.CountAsync();
            var creatorThresholdReached = creatorCount >= _options.BrandActivationCreatorThreshold;

            return Ok(new
            {
                positioningLine = _options.PositioningLine,
                phases = new
                {
                    creatorIntelligence = _options.CreatorIntelligenceEnabled,
                    creatorGraph = _options.CreatorGraphEnabled,
                    creatorGraphPublicOptIn = _options.CreatorGraphPublicOptIn,
                    brandActivation = _options.BrandActivationEnabled,
                    brandPilotInviteOnly = _options.BrandPilotInviteOnly
                },
                kpiGates = new
                {
                    activeCreatorsWeekly = creatorCount,
                    brandActivationCreatorThreshold = _options.BrandActivationCreatorThreshold,
                    creatorThresholdReached
                }
            });
        }

        [HttpPost("brand-waitlist")]
        [Authorize(Roles = "Brand,Agency,Individual,CreatorManager")]
        public async Task<IActionResult> JoinBrandWaitlist([FromBody] BrandWaitlistRequestDto dto)
        {
            if (!ModelState.IsValid)
            {
                return ValidationProblem(ModelState);
            }

            var normalizedEmail = dto.Email.Trim().ToLowerInvariant();
            var normalizedCompany = dto.CompanyName.Trim();
            var userIdClaim = User.FindFirstValue(ClaimTypes.NameIdentifier);
            var role = User.FindFirstValue(ClaimTypes.Role) ?? "Individual";
            var customerType = User.FindFirst("customer_type")?.Value ?? role;

            var exists = await _db.BrandWaitlistEntries.AnyAsync(x =>
                x.Email == normalizedEmail && x.CompanyName == normalizedCompany);

            if (exists)
            {
                _logger.LogInformation(
                    "Brand waitlist duplicate request. Email={Email}, Company={Company}, Role={Role}, CustomerType={CustomerType}",
                    normalizedEmail,
                    normalizedCompany,
                    role,
                    customerType);

                return Ok(new
                {
                    joined = true,
                    duplicate = true,
                    message = "Your brand waitlist request is already recorded."
                });
            }

            int? userId = null;
            if (int.TryParse(userIdClaim, out var parsedUserId))
            {
                userId = parsedUserId;
            }

            var entry = new BrandWaitlistEntry
            {
                Email = normalizedEmail,
                CompanyName = normalizedCompany,
                Notes = dto.Notes?.Trim(),
                Role = role,
                CustomerType = customerType,
                UserId = userId,
                Status = "Pending"
            };

            _db.BrandWaitlistEntries.Add(entry);
            await _db.SaveChangesAsync();

            _logger.LogInformation(
                "Brand waitlist joined. EntryId={EntryId}, Email={Email}, Company={Company}, Role={Role}, CustomerType={CustomerType}",
                entry.BrandWaitlistEntryId,
                entry.Email,
                entry.CompanyName,
                entry.Role,
                entry.CustomerType);

            return Ok(new
            {
                joined = true,
                duplicate = false,
                message = "You are on the brand activation waitlist. We will notify you when access opens.",
                entryId = entry.BrandWaitlistEntryId
            });
        }
    }
}
