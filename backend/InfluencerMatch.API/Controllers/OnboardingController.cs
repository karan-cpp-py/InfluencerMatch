using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Security.Claims;
using InfluencerMatch.Application.DTOs;
using InfluencerMatch.Infrastructure.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace InfluencerMatch.API.Controllers
{
    [ApiController]
    [Route("api/onboarding")]
    [Authorize]
    public class OnboardingController : ControllerBase
    {
        private readonly ApplicationDbContext _db;

        public OnboardingController(ApplicationDbContext db)
        {
            _db = db;
        }

        [HttpGet("checklist")]
        public async Task<IActionResult> Checklist()
        {
            var userId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
            var role = User.FindFirstValue(ClaimTypes.Role) ?? "Individual";

            var items = role switch
            {
                "Creator" => await CreatorChecklist(userId),
                "Brand" or "Agency" => await BrandAgencyChecklist(userId),
                _ => await IndividualChecklist(userId)
            };

            return Ok(new RoleOnboardingChecklistDto
            {
                Role = role,
                Items = items
            });
        }

        [HttpGet("demo-data")]
        public IActionResult DemoData([FromQuery] string? role = null)
        {
            var userRole = role ?? User.FindFirstValue(ClaimTypes.Role) ?? "Individual";

            if (userRole == "Creator")
            {
                return Ok(new
                {
                    mode = "creator-demo",
                    cards = new[]
                    {
                        new { title = "Weekly Growth", value = "+7.6%", hint = "Subscribers" },
                        new { title = "Potential Sponsorship Fit", value = "High", hint = "Tech + Productivity" },
                        new { title = "Best Upload Window", value = "Sat 7 PM", hint = "Audience activity peak" },
                    }
                });
            }

            return Ok(new
            {
                mode = "brand-demo",
                cards = new[]
                {
                        new { title = "Suggested Creators", value = "12", hint = "Matched to your category" },
                    new { title = "Predicted Campaign Reach", value = "1.9M", hint = "Estimated impressions" },
                    new { title = "Recommended Budget", value = "INR 2.4L", hint = "For 3 mid-tier creators" },
                }
            });
        }

        [HttpGet("brand-campaign-wizard")]
        public async Task<IActionResult> BrandCampaignWizard()
        {
            var userId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
            var role = User.FindFirstValue(ClaimTypes.Role) ?? "Individual";

            if (!string.Equals(role, "Brand", StringComparison.OrdinalIgnoreCase)
                && !string.Equals(role, "Agency", StringComparison.OrdinalIgnoreCase))
            {
                return Forbid();
            }

            var campaigns = await _db.Campaigns
                .AsNoTracking()
                .Where(x => x.BrandId == userId)
                .OrderByDescending(x => x.CampaignId)
                .Take(6)
                .ToListAsync();

            var hasCampaign = campaigns.Count > 0;
            var latest = campaigns.FirstOrDefault();
            var avgBudget = campaigns.Count > 0 ? Math.Round(campaigns.Average(x => x.Budget), 2) : 0;

            return Ok(new
            {
                hasCampaign,
                role,
                latestCampaign = latest == null ? null : new
                {
                    latest.CampaignId,
                    latest.Budget,
                    latest.Category,
                    latest.TargetLocation
                },
                guidance = new
                {
                    suggestedBudgetMin = 50000,
                    suggestedBudgetMax = 300000,
                    averagePreviousBudget = avgBudget,
                    suggestedCategories = new[] { "Technology", "Fashion", "Food", "Gaming", "Travel", "Finance" },
                    suggestedLocations = new[] { "India", "UAE", "US Metro", "Southeast Asia" }
                }
            });
        }

        private async Task<List<OnboardingChecklistItemDto>> CreatorChecklist(int userId)
        {
            var profile = await _db.CreatorProfiles.AsNoTracking().FirstOrDefaultAsync(x => x.UserId == userId);
            var hasProfile = profile != null;
            var hasChannel = profile != null && await _db.CreatorChannels.AsNoTracking().AnyAsync(x => x.CreatorProfileId == profile.CreatorProfileId);
            var hasCollab = profile != null && await _db.CollaborationRequests.AsNoTracking().AnyAsync(x => x.CreatorProfileId == profile.CreatorProfileId);

            return new List<OnboardingChecklistItemDto>
            {
                new() { Key = "profile", Title = "Complete creator profile", Completed = hasProfile },
                new() { Key = "channel", Title = "Link YouTube channel", Completed = hasChannel },
                new() { Key = "collab", Title = "Respond to first collaboration", Completed = hasCollab }
            };
        }

        private async Task<List<OnboardingChecklistItemDto>> BrandAgencyChecklist(int userId)
        {
            var hasCampaign = await _db.Campaigns.AsNoTracking().AnyAsync(x => x.BrandId == userId);
            var hasRequest = await _db.CollaborationRequests.AsNoTracking().AnyAsync(x => x.BrandUserId == userId);
            var hasSubscription = await _db.UserSubscriptions.AsNoTracking().AnyAsync(x => x.UserId == userId && x.Status == "Active");

            return new List<OnboardingChecklistItemDto>
            {
                new() { Key = "campaign", Title = "Create first campaign", Completed = hasCampaign },
                new() { Key = "request", Title = "Send first collaboration request", Completed = hasRequest },
                new() { Key = "subscription", Title = "Activate paid plan", Completed = hasSubscription }
            };
        }

        private async Task<List<OnboardingChecklistItemDto>> IndividualChecklist(int userId)
        {
            var hasSubscription = await _db.UserSubscriptions.AsNoTracking().AnyAsync(x => x.UserId == userId && x.Status == "Active");
            return new List<OnboardingChecklistItemDto>
            {
                new() { Key = "subscription", Title = "Activate a plan", Completed = hasSubscription },
                new() { Key = "search", Title = "Run first creator search", Completed = false },
                new() { Key = "request", Title = "Send first collaboration request", Completed = false }
            };
        }
    }
}
