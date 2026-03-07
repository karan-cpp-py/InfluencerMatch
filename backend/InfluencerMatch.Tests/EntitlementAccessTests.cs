using InfluencerMatch.Infrastructure.Data;
using InfluencerMatch.Infrastructure.Services;
using InfluencerMatch.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace InfluencerMatch.Tests;

public class EntitlementAccessTests
{
    [Fact]
    public async Task FreePlan_EnforcesCreatorSearchLimit()
    {
        await using var db = CreateDbContext();
        SeedPlans(db);

        db.Users.Add(new User
        {
            UserId = 11,
            Name = "Free User",
            Email = "free@test.com",
            PasswordHash = "hash",
            Role = "Brand",
            CustomerType = "Brand",
            Country = "IN"
        });

        db.UserSubscriptions.Add(new UserSubscription
        {
            UserId = 11,
            PlanId = 1,
            BillingCycle = "monthly",
            StartDate = DateTime.UtcNow.AddDays(-2),
            EndDate = DateTime.UtcNow.AddDays(20),
            Status = "Active",
            PaymentStatus = "Succeeded",
            CreatorSearchUsed = 20,
            CreatorSearchWindowStart = DateTime.UtcNow.AddDays(-2)
        });

        await db.SaveChangesAsync();

        var svc = new SubscriptionAccessService(db);
        var access = await svc.ValidateCreatorSearchAccessAsync(11);

        Assert.False(access.Allowed);
        Assert.Equal(0, access.RemainingSearches);
    }

    [Fact]
    public async Task ProfessionalPlan_AllowsAdvancedAnalytics()
    {
        await using var db = CreateDbContext();
        SeedPlans(db);

        db.Users.Add(new User
        {
            UserId = 21,
            Name = "Pro User",
            Email = "pro@test.com",
            PasswordHash = "hash",
            Role = "Agency",
            CustomerType = "Agency",
            Country = "IN"
        });

        db.UserSubscriptions.Add(new UserSubscription
        {
            UserId = 21,
            PlanId = 3,
            BillingCycle = "monthly",
            StartDate = DateTime.UtcNow.AddDays(-1),
            EndDate = DateTime.UtcNow.AddDays(29),
            Status = "Active",
            PaymentStatus = "Succeeded",
            CreatorSearchUsed = 0,
            CreatorSearchWindowStart = DateTime.UtcNow.AddDays(-1)
        });

        await db.SaveChangesAsync();

        var svc = new SubscriptionAccessService(db);
        var access = await svc.ValidateFeatureAccessAsync(21, "advanced_analytics");

        Assert.True(access.Allowed);
    }

    [Fact]
    public async Task Upgrade_ToStarter_IncreasesSearchQuota()
    {
        await using var db = CreateDbContext();
        SeedPlans(db);

        db.Users.Add(new User
        {
            UserId = 31,
            Name = "Upgrade User",
            Email = "upgrade@test.com",
            PasswordHash = "hash",
            Role = "Brand",
            CustomerType = "Brand",
            Country = "IN"
        });

        db.UserSubscriptions.Add(new UserSubscription
        {
            UserId = 31,
            PlanId = 1,
            BillingCycle = "monthly",
            StartDate = DateTime.UtcNow.AddDays(-3),
            EndDate = DateTime.UtcNow.AddDays(20),
            Status = "Active",
            PaymentStatus = "Succeeded",
            CreatorSearchUsed = 20,
            CreatorSearchWindowStart = DateTime.UtcNow.AddDays(-3)
        });

        await db.SaveChangesAsync();

        var subscriptionService = new SubscriptionService(db, new StubPaymentGatewayService());
        await subscriptionService.UpgradeAsync(31, new InfluencerMatch.Application.DTOs.UpgradeSubscriptionRequestDto
        {
            PlanId = 2,
            BillingCycle = "monthly",
            PaymentProvider = "stripe",
            Currency = "INR"
        }, "idem-upgrade-1");

        var accessService = new SubscriptionAccessService(db);
        var access = await accessService.ValidateCreatorSearchAccessAsync(31);
        Assert.True(access.Allowed);
        Assert.True(access.RemainingSearches >= 99);
    }

    private static ApplicationDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString("N"))
            .Options;
        return new ApplicationDbContext(options);
    }

    private static void SeedPlans(ApplicationDbContext db)
    {
        db.SubscriptionPlans.AddRange(
            new SubscriptionPlan { PlanId = 1, PlanName = "Free", PriceMonthly = 0, PriceYearly = 0, MaxCreatorSearch = 20, ExportAllowed = false, AnalyticsAccessLevel = "Basic" },
            new SubscriptionPlan { PlanId = 2, PlanName = "Starter", PriceMonthly = 1999, PriceYearly = 23988, MaxCreatorSearch = 100, ExportAllowed = false, AnalyticsAccessLevel = "Standard" },
            new SubscriptionPlan { PlanId = 3, PlanName = "Professional", PriceMonthly = 7999, PriceYearly = 95988, MaxCreatorSearch = null, ExportAllowed = true, AnalyticsAccessLevel = "Advanced" },
            new SubscriptionPlan { PlanId = 4, PlanName = "Enterprise", PriceMonthly = 0, PriceYearly = 0, MaxCreatorSearch = null, ExportAllowed = true, AnalyticsAccessLevel = "Custom" }
        );
        db.SaveChanges();
    }

    private sealed class StubPaymentGatewayService : InfluencerMatch.Application.Interfaces.IPaymentGatewayService
    {
        public Task<InfluencerMatch.Application.DTOs.PaymentInitResultDto> InitializePaymentAsync(string provider, decimal amount, string currency, string description)
            => Task.FromResult(new InfluencerMatch.Application.DTOs.PaymentInitResultDto
            {
                Provider = provider,
                PaymentStatus = "Succeeded",
                ProviderPaymentId = "pi_upgrade_123",
                Message = "ok"
            });
    }
}
