using InfluencerMatch.Application.DTOs;
using InfluencerMatch.Application.Interfaces;
using InfluencerMatch.Domain.Entities;
using InfluencerMatch.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace InfluencerMatch.Infrastructure.Services
{
    public class SubscriptionAccessService : ISubscriptionAccessService
    {
        private static readonly HashSet<string> AllowedSearchCustomerTypes =
            new(StringComparer.OrdinalIgnoreCase)
            {
                "Brand",
                "Agency",
                "Individual",
                "CreatorManager"
            };

        private readonly ApplicationDbContext _db;

        public SubscriptionAccessService(ApplicationDbContext db)
        {
            _db = db;
        }

        public async Task<SearchAccessResultDto> ValidateCreatorSearchAccessAsync(int userId)
        {
            var user = await _db.Users.AsNoTracking().FirstOrDefaultAsync(u => u.UserId == userId);
            if (user == null)
            {
                return new SearchAccessResultDto { Allowed = false, Reason = "User not found." };
            }

            if (!AllowedSearchCustomerTypes.Contains(user.CustomerType))
            {
                return new SearchAccessResultDto
                {
                    Allowed = false,
                    Reason = "Your customer type does not have creator discovery access."
                };
            }

            var now = DateTime.UtcNow;
            var subscription = await _db.UserSubscriptions
                .Include(s => s.Plan)
                .Where(s => s.UserId == userId && s.Status == "Active" && s.EndDate > now)
                .OrderByDescending(s => s.CreatedAt)
                .FirstOrDefaultAsync();

            if (subscription == null)
            {
                subscription = await EnsureFreeSubscriptionAsync(userId, now);
            }

            if (subscription == null)
            {
                return new SearchAccessResultDto
                {
                    Allowed = false,
                    Reason = "No active subscription found.",
                    RequiredPlan = "Starter",
                    CurrentPlan = null,
                    ErrorCode = "SUBSCRIPTION_REQUIRED"
                };
            }

            if (subscription.PaymentStatus != "Succeeded")
            {
                return new SearchAccessResultDto
                {
                    Allowed = false,
                    Reason = "Payment is pending for the current subscription.",
                    RequiredPlan = subscription.Plan?.PlanName,
                    CurrentPlan = subscription.Plan?.PlanName,
                    ErrorCode = "PAYMENT_PENDING"
                };
            }

            if (subscription.Plan?.MaxCreatorSearch == null)
            {
                return new SearchAccessResultDto { Allowed = true };
            }

            if (subscription.CreatorSearchWindowStart.AddMonths(1) <= now)
            {
                subscription.CreatorSearchWindowStart = now;
                subscription.CreatorSearchUsed = 0;
                await _db.SaveChangesAsync();
            }

            var remaining = subscription.Plan.MaxCreatorSearch.Value - subscription.CreatorSearchUsed;
            if (remaining <= 0)
            {
                return new SearchAccessResultDto
                {
                    Allowed = false,
                    Reason = "You have reached your plan limit. Upgrade to continue.",
                    RemainingSearches = 0,
                    RequiredPlan = NextPlanFor(subscription.Plan.PlanName),
                    CurrentPlan = subscription.Plan.PlanName,
                    ErrorCode = "PLAN_LIMIT_REACHED"
                };
            }

            return new SearchAccessResultDto
            {
                Allowed = true,
                RemainingSearches = remaining
            };
        }

        public async Task<FeatureAccessResultDto> ValidateFeatureAccessAsync(int userId, string featureKey)
        {
            var now = DateTime.UtcNow;
            var subscription = await _db.UserSubscriptions
                .Include(s => s.Plan)
                .Where(s => s.UserId == userId && s.Status == "Active" && s.EndDate > now)
                .OrderByDescending(s => s.CreatedAt)
                .FirstOrDefaultAsync();

            if (subscription == null)
            {
                subscription = await EnsureFreeSubscriptionAsync(userId, now);
            }

            if (subscription?.Plan == null)
            {
                return new FeatureAccessResultDto
                {
                    Allowed = false,
                    Reason = "No active subscription found.",
                    RequiredPlan = "Starter",
                    ErrorCode = "SUBSCRIPTION_REQUIRED"
                };
            }

            if (subscription.PaymentStatus != "Succeeded")
            {
                return new FeatureAccessResultDto
                {
                    Allowed = false,
                    Reason = "Payment is pending for your current subscription.",
                    RequiredPlan = subscription.Plan.PlanName,
                    CurrentPlan = subscription.Plan.PlanName,
                    ErrorCode = "PAYMENT_PENDING"
                };
            }

            var plan = subscription.Plan.PlanName;
            var allowed = featureKey switch
            {
                "advanced_analytics" => plan is "Starter" or "Professional" or "Enterprise",
                "exports" => subscription.Plan.ExportAllowed,
                "campaign_tools" => plan is "Starter" or "Professional" or "Enterprise",
                _ => true
            };

            if (allowed)
            {
                return new FeatureAccessResultDto { Allowed = true };
            }

            var requiredPlan = featureKey switch
            {
                "advanced_analytics" => "Starter",
                "exports" => "Professional",
                "campaign_tools" => "Starter",
                _ => "Starter"
            };

            return new FeatureAccessResultDto
            {
                Allowed = false,
                RequiredPlan = requiredPlan,
                CurrentPlan = plan,
                ErrorCode = "PLAN_LIMIT_REACHED",
                Reason = "You have reached your plan limit. Upgrade to continue."
            };
        }

        private static string? NextPlanFor(string? currentPlan)
        {
            return currentPlan?.Trim() switch
            {
                "Free" => "Starter",
                "Starter" => "Professional",
                "Professional" => "Enterprise",
                _ => "Starter"
            };
        }

        public async Task RecordCreatorSearchUsageAsync(int userId)
        {
            var now = DateTime.UtcNow;
            var subscription = await _db.UserSubscriptions
                .Include(s => s.Plan)
                .Where(s => s.UserId == userId && s.Status == "Active" && s.EndDate > now)
                .OrderByDescending(s => s.CreatedAt)
                .FirstOrDefaultAsync();

            if (subscription == null)
            {
                subscription = await EnsureFreeSubscriptionAsync(userId, now);
            }

            if (subscription?.Plan?.MaxCreatorSearch == null)
            {
                return;
            }

            if (subscription.CreatorSearchWindowStart.AddMonths(1) <= now)
            {
                subscription.CreatorSearchWindowStart = now;
                subscription.CreatorSearchUsed = 0;
            }

            subscription.CreatorSearchUsed += 1;
            await _db.SaveChangesAsync();
        }

        private async Task<UserSubscription?> EnsureFreeSubscriptionAsync(int userId, DateTime now)
        {
            var freePlan = await _db.SubscriptionPlans
                .FirstOrDefaultAsync(p => p.PlanName == "Free");

            if (freePlan == null)
            {
                return null;
            }

            var freeSubscription = new UserSubscription
            {
                UserId = userId,
                PlanId = freePlan.PlanId,
                BillingCycle = "monthly",
                StartDate = now,
                EndDate = now.AddMonths(1),
                Status = "Active",
                PaymentStatus = "Succeeded",
                CreatorSearchUsed = 0,
                CreatorSearchWindowStart = now,
                CreatedAt = now
            };

            _db.UserSubscriptions.Add(freeSubscription);
            await _db.SaveChangesAsync();

            return await _db.UserSubscriptions
                .Include(s => s.Plan)
                .FirstOrDefaultAsync(s => s.SubscriptionId == freeSubscription.SubscriptionId);
        }
    }
}
