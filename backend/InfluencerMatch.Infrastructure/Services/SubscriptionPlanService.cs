using InfluencerMatch.Application.DTOs;
using InfluencerMatch.Application.Interfaces;
using InfluencerMatch.Domain.Entities;
using InfluencerMatch.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace InfluencerMatch.Infrastructure.Services
{
    public class SubscriptionPlanService : ISubscriptionPlanService
    {
        private readonly ApplicationDbContext _db;

        public SubscriptionPlanService(ApplicationDbContext db)
        {
            _db = db;
        }

        public async Task<List<SubscriptionPlanDto>> GetPlansAsync()
        {
            return await _db.SubscriptionPlans
                .AsNoTracking()
                .OrderBy(p => p.PriceMonthly)
                .Select(p => new SubscriptionPlanDto
                {
                    PlanId = p.PlanId,
                    PlanName = p.PlanName,
                    PriceMonthly = p.PriceMonthly,
                    PriceYearly = p.PriceYearly,
                    MaxCreatorSearch = p.MaxCreatorSearch,
                    ExportAllowed = p.ExportAllowed,
                    AnalyticsAccessLevel = p.AnalyticsAccessLevel
                })
                .ToListAsync();
        }

        public async Task SeedDefaultPlansAsync()
        {
            if (await _db.SubscriptionPlans.AnyAsync())
            {
                return;
            }

            var now = DateTime.UtcNow;
            _db.SubscriptionPlans.AddRange(
                new SubscriptionPlan
                {
                    PlanName = "Free",
                    PriceMonthly = 0,
                    PriceYearly = 0,
                    MaxCreatorSearch = 20,
                    ExportAllowed = false,
                    AnalyticsAccessLevel = "Basic",
                    CreatedAt = now
                },
                new SubscriptionPlan
                {
                    PlanName = "Starter",
                    PriceMonthly = 1999,
                    PriceYearly = 1999 * 12,
                    MaxCreatorSearch = 100,
                    ExportAllowed = false,
                    AnalyticsAccessLevel = "Standard",
                    CreatedAt = now
                },
                new SubscriptionPlan
                {
                    PlanName = "Professional",
                    PriceMonthly = 7999,
                    PriceYearly = 7999 * 12,
                    MaxCreatorSearch = null,
                    ExportAllowed = true,
                    AnalyticsAccessLevel = "Advanced",
                    CreatedAt = now
                },
                new SubscriptionPlan
                {
                    PlanName = "Enterprise",
                    PriceMonthly = 0,
                    PriceYearly = 0,
                    MaxCreatorSearch = null,
                    ExportAllowed = true,
                    AnalyticsAccessLevel = "Custom",
                    CreatedAt = now
                });

            await _db.SaveChangesAsync();
        }
    }
}
