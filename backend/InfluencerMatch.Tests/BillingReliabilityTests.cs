using InfluencerMatch.Application.DTOs;
using InfluencerMatch.Application.Interfaces;
using InfluencerMatch.Domain.Entities;
using InfluencerMatch.Infrastructure.Data;
using InfluencerMatch.Infrastructure.Services;
using InfluencerMatch.Infrastructure.Services.PaymentProviders;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Xunit;

namespace InfluencerMatch.Tests;

public class BillingReliabilityTests
{
    [Fact]
    public async Task Subscribe_WithSameIdempotencyKey_ReturnsOriginalResponse()
    {
        await using var db = CreateDbContext();
        SeedBillingState(db);

        var svc = new SubscriptionService(db, new StubPaymentGatewayService());
        var request = new SubscribeRequestDto
        {
            PlanId = 2,
            BillingCycle = "monthly",
            PaymentProvider = "stripe",
            Currency = "INR"
        };

        var first = await svc.SubscribeAsync(1, request, "idem-123");
        var second = await svc.SubscribeAsync(1, request, "idem-123");

        Assert.Equal(first.SubscriptionId, second.SubscriptionId);
        Assert.Equal(first.ProviderPaymentId, second.ProviderPaymentId);
        Assert.Single(db.IdempotencyRecords.Where(x => x.UserId == 1 && x.Scope == "checkout.create"));
    }

    [Fact]
    public async Task DuplicateWebhookEvent_IsRejectedAsReplay()
    {
        await using var db = CreateDbContext();
        SeedBillingState(db);

        var now = DateTime.UtcNow;
        db.UserSubscriptions.Add(new UserSubscription
        {
            SubscriptionId = 9,
            UserId = 1,
            PlanId = 2,
            BillingCycle = "monthly",
            StartDate = now,
            EndDate = now.AddMonths(1),
            Status = "Active",
            PaymentStatus = "Pending"
        });
        db.PaymentRecords.Add(new PaymentRecord
        {
            SubscriptionId = 9,
            Provider = "stripe",
            ProviderPaymentId = "pi_123",
            Amount = 100,
            Currency = "INR",
            PaymentStatus = "Pending"
        });
        await db.SaveChangesAsync();

        var options = Options.Create(new PaymentProviderOptions
        {
            Stripe = new ProviderConfig { WebhookSecret = "whsec_test" }
        });

        var webhookService = new PaymentWebhookService(
            db,
            options,
            new DummyHttpClientFactory(),
            NullLogger<PaymentWebhookService>.Instance);

        var payload = "{\"id\":\"evt_1\",\"type\":\"payment_intent.succeeded\",\"data\":{\"object\":{\"id\":\"pi_123\"}}}";
        var timestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds().ToString();
        var sig = ComputeStripeSig("whsec_test", $"{timestamp}.{payload}");

        var headers = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["Stripe-Signature"] = $"t={timestamp},v1={sig}"
        };

        var first = await webhookService.ProcessWebhookAsync("stripe", payload, headers);
        Assert.True(first);

        await Assert.ThrowsAsync<UnauthorizedAccessException>(() =>
            webhookService.ProcessWebhookAsync("stripe", payload, headers));
    }

    private static ApplicationDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString("N"))
            .Options;
        return new ApplicationDbContext(options);
    }

    private static void SeedBillingState(ApplicationDbContext db)
    {
        db.Users.Add(new User
        {
            UserId = 1,
            Name = "Brand User",
            Email = "brand@test.com",
            PasswordHash = "hash",
            Role = "Brand",
            CustomerType = "Brand",
            Country = "IN",
            CreatedAt = DateTime.UtcNow
        });

        db.SubscriptionPlans.AddRange(
            new SubscriptionPlan
            {
                PlanId = 1,
                PlanName = "Free",
                PriceMonthly = 0,
                PriceYearly = 0,
                MaxCreatorSearch = 20,
                ExportAllowed = false,
                AnalyticsAccessLevel = "Basic"
            },
            new SubscriptionPlan
            {
                PlanId = 2,
                PlanName = "Starter",
                PriceMonthly = 1999,
                PriceYearly = 1999 * 12,
                MaxCreatorSearch = 100,
                ExportAllowed = false,
                AnalyticsAccessLevel = "Standard"
            });

        db.SaveChanges();
    }

    private static string ComputeStripeSig(string secret, string message)
    {
        using var hmac = new System.Security.Cryptography.HMACSHA256(System.Text.Encoding.UTF8.GetBytes(secret));
        return Convert.ToHexString(hmac.ComputeHash(System.Text.Encoding.UTF8.GetBytes(message))).ToLowerInvariant();
    }

    private sealed class StubPaymentGatewayService : IPaymentGatewayService
    {
        public Task<PaymentInitResultDto> InitializePaymentAsync(string provider, decimal amount, string currency, string description)
        {
            return Task.FromResult(new PaymentInitResultDto
            {
                Provider = provider,
                PaymentStatus = amount == 0 ? "Succeeded" : "Pending",
                ProviderPaymentId = "pi_stub_123",
                Message = "ok"
            });
        }
    }

    private sealed class DummyHttpClientFactory : IHttpClientFactory
    {
        public HttpClient CreateClient(string name) => new();
    }
}
