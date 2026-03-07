using InfluencerMatch.Application.DTOs;
using InfluencerMatch.Application.Interfaces;
using InfluencerMatch.Domain.Entities;
using InfluencerMatch.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace InfluencerMatch.Infrastructure.Services
{
    public class SubscriptionService : ISubscriptionService
    {
        private const int GracePeriodDays = 3;

        private readonly ApplicationDbContext _db;
        private readonly IPaymentGatewayService _paymentGateway;
        private readonly INotificationService _notifications;

        public SubscriptionService(
            ApplicationDbContext db,
            IPaymentGatewayService paymentGateway,
            INotificationService notifications)
        {
            _db = db;
            _paymentGateway = paymentGateway;
            _notifications = notifications;
        }

        public Task<SubscriptionResponseDto> SubscribeAsync(int userId, SubscribeRequestDto request, string? idempotencyKey = null)
            => CreateOrReplaceSubscriptionAsync(userId, request, idempotencyKey, "checkout.create");

        public Task<SubscriptionResponseDto> UpgradeAsync(int userId, UpgradeSubscriptionRequestDto request, string? idempotencyKey = null)
            => CreateOrReplaceSubscriptionAsync(userId, request, idempotencyKey, "subscription.upgrade");

        public async Task<SubscriptionResponseDto> CancelAsync(int userId, CancelSubscriptionRequestDto request, string? idempotencyKey = null)
        {
            var requestHash = ComputeRequestHash(request);
            var cached = await TryReadIdempotentResponseAsync(userId, "subscription.cancel", idempotencyKey, requestHash);
            if (cached != null)
            {
                return cached;
            }

            var now = DateTime.UtcNow;
            var subscription = await _db.UserSubscriptions
                .Include(s => s.Plan)
                .Where(s => s.UserId == userId && s.Status == "Active" && s.EndDate > now)
                .OrderByDescending(s => s.CreatedAt)
                .FirstOrDefaultAsync()
                ?? throw new InvalidOperationException("No active subscription found.");

            subscription.Status = "Cancelled";
            subscription.CancelAtPeriodEnd = true;
            subscription.CancelledAt = now;
            subscription.PaymentStatus = "Cancelled";
            subscription.GracePeriodEndsAt = null;

            var response = Map(subscription, subscription.Plan!);

            await _notifications.NotifyAsync(new NotificationCreateRequestDto
            {
                UserId = userId,
                Type = "subscription.cancelled",
                Title = "Subscription cancellation scheduled",
                Message = "Your subscription will end at period close. You can reactivate anytime before expiry.",
                SendEmail = true
            });

            await StoreIdempotentResponseAsync(userId, "subscription.cancel", idempotencyKey, requestHash, response, 200);
            await _db.SaveChangesAsync();

            return response;
        }

        public async Task<SubscriptionResponseDto> ReactivateAsync(int userId, ReactivateSubscriptionRequestDto request, string? idempotencyKey = null)
        {
            var requestHash = ComputeRequestHash(request);
            var cached = await TryReadIdempotentResponseAsync(userId, "subscription.reactivate", idempotencyKey, requestHash);
            if (cached != null)
            {
                return cached;
            }

            var now = DateTime.UtcNow;
            var subscription = await _db.UserSubscriptions
                .Include(s => s.Plan)
                .Where(s => s.UserId == userId && s.EndDate > now)
                .OrderByDescending(s => s.CreatedAt)
                .FirstOrDefaultAsync()
                ?? throw new InvalidOperationException("No subscription found to reactivate.");

            subscription.Status = "Active";
            subscription.CancelAtPeriodEnd = false;
            subscription.CancelledAt = null;
            subscription.PaymentStatus = "Succeeded";
            subscription.GracePeriodEndsAt = null;
            subscription.PaymentRetryCount = 0;

            var response = Map(subscription, subscription.Plan!);
            await StoreIdempotentResponseAsync(userId, "subscription.reactivate", idempotencyKey, requestHash, response, 200);

            await _notifications.NotifyAsync(new NotificationCreateRequestDto
            {
                UserId = userId,
                Type = "subscription.reactivated",
                Title = "Subscription reactivated",
                Message = "Auto-renew is restored and your plan remains active.",
                SendEmail = true
            });

            await _db.SaveChangesAsync();
            return response;
        }

        public async Task<SubscriptionResponseDto> UpdatePaymentMethodAsync(int userId, UpdatePaymentMethodRequestDto request, string? idempotencyKey = null)
        {
            var requestHash = ComputeRequestHash(request);
            var cached = await TryReadIdempotentResponseAsync(userId, "subscription.update-payment-method", idempotencyKey, requestHash);
            if (cached != null)
            {
                return cached;
            }

            var now = DateTime.UtcNow;
            var subscription = await _db.UserSubscriptions
                .Include(s => s.Plan)
                .Where(s => s.UserId == userId && s.EndDate > now)
                .OrderByDescending(s => s.CreatedAt)
                .FirstOrDefaultAsync()
                ?? throw new InvalidOperationException("No active subscription found.");

            subscription.PaymentMethodBrand = string.IsNullOrWhiteSpace(request.Brand) ? "Card" : request.Brand.Trim();
            subscription.PaymentMethodLast4 = request.Last4?.Trim();

            var response = Map(subscription, subscription.Plan!);
            await StoreIdempotentResponseAsync(userId, "subscription.update-payment-method", idempotencyKey, requestHash, response, 200);

            await _notifications.NotifyAsync(new NotificationCreateRequestDto
            {
                UserId = userId,
                Type = "billing.payment_method.updated",
                Title = "Payment method updated",
                Message = "Your default payment method has been updated successfully.",
                SendEmail = true
            });

            await _db.SaveChangesAsync();
            return response;
        }

        private async Task<SubscriptionResponseDto> CreateOrReplaceSubscriptionAsync(
            int userId,
            SubscribeRequestDto request,
            string? idempotencyKey,
            string scope)
        {
            var requestHash = ComputeRequestHash(request);
            var cached = await TryReadIdempotentResponseAsync(userId, scope, idempotencyKey, requestHash);
            if (cached != null)
            {
                return cached;
            }

            var user = await _db.Users.FirstOrDefaultAsync(u => u.UserId == userId)
                ?? throw new InvalidOperationException("User not found.");

            var cycle = request.BillingCycle.Trim().ToLowerInvariant();
            if (cycle is not ("monthly" or "yearly"))
            {
                throw new InvalidOperationException("BillingCycle must be monthly or yearly.");
            }

            var plan = await _db.SubscriptionPlans.FirstOrDefaultAsync(p => p.PlanId == request.PlanId)
                ?? throw new InvalidOperationException("Subscription plan not found.");

            var active = await _db.UserSubscriptions
                .Where(s => s.UserId == userId && s.Status == "Active")
                .ToListAsync();

            foreach (var item in active)
            {
                item.Status = "Expired";
            }

            var amount = cycle == "yearly" ? plan.PriceYearly : plan.PriceMonthly;
            var payment = await _paymentGateway.InitializePaymentAsync(
                request.PaymentProvider,
                amount,
                request.Currency,
                $"{plan.PlanName} {cycle} subscription");

            var now = DateTime.UtcNow;
            var subscription = new UserSubscription
            {
                UserId = userId,
                PlanId = plan.PlanId,
                BillingCycle = cycle,
                StartDate = now,
                EndDate = cycle == "yearly" ? now.AddYears(1) : now.AddMonths(1),
                Status = "Active",
                PaymentStatus = payment.PaymentStatus,
                GracePeriodEndsAt = null,
                PaymentRetryCount = 0,
                CreatorSearchUsed = 0,
                CreatorSearchWindowStart = now,
                CreatedAt = now
            };

            _db.UserSubscriptions.Add(subscription);
            await _db.SaveChangesAsync();

            _db.PaymentRecords.Add(new PaymentRecord
            {
                SubscriptionId = subscription.SubscriptionId,
                Provider = payment.Provider,
                Amount = amount,
                Currency = request.Currency,
                PaymentStatus = payment.PaymentStatus,
                ProviderPaymentId = payment.ProviderPaymentId,
                CreatedAt = now
            });

            _db.Invoices.Add(new Invoice
            {
                UserId = userId,
                SubscriptionId = subscription.SubscriptionId,
                Amount = amount,
                Currency = request.Currency,
                Status = payment.PaymentStatus,
                Provider = payment.Provider,
                ProviderInvoiceId = payment.ProviderPaymentId,
                CreatedAt = now
            });

            var response = Map(
                subscription,
                plan,
                paymentProvider: payment.Provider,
                providerPaymentId: payment.ProviderPaymentId,
                paymentMessage: payment.Message);

            await StoreIdempotentResponseAsync(userId, scope, idempotencyKey, requestHash, response, 200);

            await _notifications.NotifyAsync(new NotificationCreateRequestDto
            {
                UserId = userId,
                Type = "subscription.started",
                Title = "Subscription activated",
                Message = $"You are now on {plan.PlanName} ({cycle}).",
                SendEmail = true
            });

            await _db.SaveChangesAsync();
            return response;
        }

        public async Task<SubscriptionResponseDto?> GetCurrentSubscriptionAsync(int userId)
        {
            var now = DateTime.UtcNow;
            var subscription = await _db.UserSubscriptions
                .Include(s => s.Plan)
                .Where(s => s.UserId == userId && (s.Status == "Active" || s.Status == "GracePeriod"))
                .OrderByDescending(s => s.CreatedAt)
                .FirstOrDefaultAsync();

            if (subscription == null)
            {
                return null;
            }

            await ProcessPaymentRecoveryAsync(userId, subscription, now);

            subscription = await _db.UserSubscriptions
                .Include(s => s.Plan)
                .Where(s => s.UserId == userId && (s.Status == "Active" || s.Status == "GracePeriod"))
                .OrderByDescending(s => s.CreatedAt)
                .FirstOrDefaultAsync();

            if (subscription == null)
            {
                return null;
            }

            var latestPayment = await _db.PaymentRecords
                .AsNoTracking()
                .Where(p => p.SubscriptionId == subscription.SubscriptionId)
                .OrderByDescending(p => p.CreatedAt)
                .FirstOrDefaultAsync();

            await EmitLifecycleNotificationsAsync(userId, subscription);

            return Map(
                subscription,
                subscription.Plan!,
                paymentProvider: latestPayment?.Provider,
                providerPaymentId: latestPayment?.ProviderPaymentId,
                paymentMessage: null);
        }

        public async Task<SubscriptionRecoveryStatusDto?> GetRecoveryStatusAsync(int userId)
        {
            var now = DateTime.UtcNow;
            var subscription = await _db.UserSubscriptions
                .AsNoTracking()
                .Where(s => s.UserId == userId && (s.Status == "Active" || s.Status == "GracePeriod"))
                .OrderByDescending(s => s.CreatedAt)
                .FirstOrDefaultAsync();

            if (subscription == null)
            {
                return null;
            }

            var inGrace = subscription.Status == "GracePeriod"
                && subscription.GracePeriodEndsAt.HasValue
                && subscription.GracePeriodEndsAt.Value > now;

            var daysRemaining = inGrace
                ? Math.Max(0, (int)Math.Ceiling((subscription.GracePeriodEndsAt!.Value - now).TotalDays))
                : 0;

            var action = inGrace
                ? "Update payment method and retry payment to keep premium access."
                : subscription.PaymentStatus == "Failed"
                    ? "Retry payment to restore full access."
                    : "Subscription healthy.";

            return new SubscriptionRecoveryStatusDto
            {
                InGracePeriod = inGrace,
                GracePeriodEndsAt = subscription.GracePeriodEndsAt,
                GraceDaysRemaining = daysRemaining,
                PaymentRetryCount = subscription.PaymentRetryCount,
                CurrentPaymentStatus = subscription.PaymentStatus,
                SuggestedAction = action
            };
        }

        public async Task<SubscriptionResponseDto> RetryPaymentRecoveryAsync(int userId, string? idempotencyKey = null)
        {
            var requestHash = ComputeRequestHash(new { action = "retry-payment-recovery" });
            var cached = await TryReadIdempotentResponseAsync(userId, "subscription.retry-payment", idempotencyKey, requestHash);
            if (cached != null)
            {
                return cached;
            }

            var now = DateTime.UtcNow;
            var subscription = await _db.UserSubscriptions
                .Include(s => s.Plan)
                .Where(s => s.UserId == userId && (s.Status == "Active" || s.Status == "GracePeriod"))
                .OrderByDescending(s => s.CreatedAt)
                .FirstOrDefaultAsync()
                ?? throw new InvalidOperationException("No subscription found for payment retry.");

            if (string.IsNullOrWhiteSpace(subscription.PaymentMethodLast4))
            {
                throw new InvalidOperationException("Add a payment method before retrying recovery payment.");
            }

            if (subscription.Plan == null)
            {
                throw new InvalidOperationException("Subscription plan not found.");
            }

            subscription.PaymentRetryCount += 1;
            subscription.LastPaymentRetryAt = now;
            subscription.Status = "Active";
            subscription.PaymentStatus = "Succeeded";
            subscription.GracePeriodEndsAt = null;
            subscription.EndDate = NextCycleEnd(now, subscription.BillingCycle);

            var amount = subscription.BillingCycle == "yearly"
                ? subscription.Plan.PriceYearly
                : subscription.Plan.PriceMonthly;

            _db.PaymentRecords.Add(new PaymentRecord
            {
                SubscriptionId = subscription.SubscriptionId,
                Provider = "recovery-retry",
                Amount = amount,
                Currency = "INR",
                PaymentStatus = "Succeeded",
                ProviderPaymentId = $"retry-{Guid.NewGuid():N}",
                CreatedAt = now
            });

            _db.Invoices.Add(new Invoice
            {
                UserId = userId,
                SubscriptionId = subscription.SubscriptionId,
                Amount = amount,
                Currency = "INR",
                Status = "Succeeded",
                Provider = "recovery-retry",
                ProviderInvoiceId = $"retry-{Guid.NewGuid():N}",
                CreatedAt = now
            });

            var response = Map(subscription, subscription.Plan);
            await StoreIdempotentResponseAsync(userId, "subscription.retry-payment", idempotencyKey, requestHash, response, 200);

            await _notifications.NotifyAsync(new NotificationCreateRequestDto
            {
                UserId = userId,
                Type = "billing.recovery.success",
                Title = "Payment recovered",
                Message = "Your payment retry succeeded and premium access is restored.",
                SendEmail = true
            });

            await _db.SaveChangesAsync();
            return response;
        }

        public async Task<List<InvoiceListItemDto>> GetInvoicesAsync(int userId)
        {
            return await _db.Invoices
                .AsNoTracking()
                .Where(i => i.UserId == userId)
                .OrderByDescending(i => i.CreatedAt)
                .Select(i => new InvoiceListItemDto
                {
                    InvoiceId = i.InvoiceId,
                    Amount = i.Amount,
                    Currency = i.Currency,
                    Status = i.Status,
                    Provider = i.Provider,
                    ProviderInvoiceId = i.ProviderInvoiceId,
                    CreatedAt = i.CreatedAt
                })
                .ToListAsync();
        }

        public async Task<string?> GetReceiptAsync(int userId, int invoiceId)
        {
            var invoice = await _db.Invoices
                .AsNoTracking()
                .FirstOrDefaultAsync(i => i.InvoiceId == invoiceId && i.UserId == userId);
            if (invoice == null)
            {
                return null;
            }

            return $"Receipt #{invoice.InvoiceId}\nProvider: {invoice.Provider}\nReference: {invoice.ProviderInvoiceId}\nAmount: {invoice.Currency} {invoice.Amount:F2}\nStatus: {invoice.Status}\nDate: {invoice.CreatedAt:u}";
        }

        public async Task<BillingSummaryDto?> GetBillingSummaryAsync(int userId, int? targetPlanId = null, string? billingCycle = null)
        {
            var now = DateTime.UtcNow;
            var current = await _db.UserSubscriptions
                .Include(s => s.Plan)
                .Where(s => s.UserId == userId && s.EndDate > now)
                .OrderByDescending(s => s.CreatedAt)
                .FirstOrDefaultAsync();

            if (current == null || current.Plan == null)
            {
                return null;
            }

            var cycle = string.IsNullOrWhiteSpace(billingCycle) ? current.BillingCycle : billingCycle.Trim().ToLowerInvariant();
            var currentAmount = cycle == "yearly" ? current.Plan.PriceYearly : current.Plan.PriceMonthly;

            decimal? proration = null;
            if (targetPlanId.HasValue)
            {
                var target = await _db.SubscriptionPlans.AsNoTracking().FirstOrDefaultAsync(x => x.PlanId == targetPlanId.Value);
                if (target != null)
                {
                    var targetAmount = cycle == "yearly" ? target.PriceYearly : target.PriceMonthly;
                    var remainingDays = Math.Max(0, (current.EndDate - now).TotalDays);
                    var cycleDays = cycle == "yearly" ? 365.0 : 30.0;
                    var fraction = Math.Clamp(remainingDays / cycleDays, 0.0, 1.0);
                    proration = Math.Round((targetAmount - currentAmount) * (decimal)fraction, 2);
                }
            }

            return new BillingSummaryDto
            {
                CurrentPlanName = current.Plan.PlanName,
                BillingCycle = cycle,
                NextBillingDate = current.EndDate,
                CurrentCycleAmount = currentAmount,
                ProrationPreviewAmount = proration,
                Currency = "INR"
            };
        }

        private async Task<SubscriptionResponseDto?> TryReadIdempotentResponseAsync(
            int userId,
            string scope,
            string? idempotencyKey,
            string requestHash)
        {
            if (string.IsNullOrWhiteSpace(idempotencyKey))
            {
                return null;
            }

            var existing = await _db.IdempotencyRecords
                .AsNoTracking()
                .FirstOrDefaultAsync(x =>
                    x.UserId == userId
                    && x.Scope == scope
                    && x.IdempotencyKey == idempotencyKey);

            if (existing == null)
            {
                return null;
            }

            if (!string.Equals(existing.RequestHash, requestHash, StringComparison.Ordinal))
            {
                throw new InvalidOperationException("Idempotency key already used with a different request payload.");
            }

            return JsonSerializer.Deserialize<SubscriptionResponseDto>(existing.ResponseJson);
        }

        private Task StoreIdempotentResponseAsync(
            int userId,
            string scope,
            string? idempotencyKey,
            string requestHash,
            SubscriptionResponseDto response,
            int statusCode)
        {
            if (string.IsNullOrWhiteSpace(idempotencyKey))
            {
                return Task.CompletedTask;
            }

            _db.IdempotencyRecords.Add(new IdempotencyRecord
            {
                UserId = userId,
                Scope = scope,
                IdempotencyKey = idempotencyKey,
                RequestHash = requestHash,
                ResponseJson = JsonSerializer.Serialize(response),
                StatusCode = statusCode,
                CreatedAt = DateTime.UtcNow,
                ExpiresAt = DateTime.UtcNow.AddDays(3)
            });

            return Task.CompletedTask;
        }

        private static string ComputeRequestHash(object payload)
        {
            var json = JsonSerializer.Serialize(payload);
            var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(json));
            return Convert.ToHexString(bytes);
        }

        private static SubscriptionResponseDto Map(
            UserSubscription subscription,
            SubscriptionPlan plan,
            string? paymentProvider = null,
            string? providerPaymentId = null,
            string? paymentMessage = null)
        {
            return new SubscriptionResponseDto
            {
                SubscriptionId = subscription.SubscriptionId,
                UserId = subscription.UserId,
                BillingCycle = subscription.BillingCycle,
                StartDate = subscription.StartDate,
                EndDate = subscription.EndDate,
                Status = subscription.Status,
                PaymentStatus = subscription.PaymentStatus,
                CreatorSearchUsed = subscription.CreatorSearchUsed,
                PaymentProvider = paymentProvider,
                ProviderPaymentId = providerPaymentId,
                PaymentMessage = paymentMessage,
                AutoRenew = subscription.AutoRenew,
                CancelAtPeriodEnd = subscription.CancelAtPeriodEnd,
                CancelledAt = subscription.CancelledAt,
                PaymentMethodDisplay = string.IsNullOrWhiteSpace(subscription.PaymentMethodLast4)
                    ? null
                    : $"{subscription.PaymentMethodBrand ?? "Card"} ****{subscription.PaymentMethodLast4}",
                NextBillingDate = subscription.EndDate,
                GracePeriodEndsAt = subscription.GracePeriodEndsAt,
                PaymentRetryCount = subscription.PaymentRetryCount,
                LastPaymentRetryAt = subscription.LastPaymentRetryAt,
                Plan = new SubscriptionPlanDto
                {
                    PlanId = plan.PlanId,
                    PlanName = plan.PlanName,
                    PriceMonthly = plan.PriceMonthly,
                    PriceYearly = plan.PriceYearly,
                    MaxCreatorSearch = plan.MaxCreatorSearch,
                    ExportAllowed = plan.ExportAllowed,
                    AnalyticsAccessLevel = plan.AnalyticsAccessLevel
                }
            };
        }

        private async Task ProcessPaymentRecoveryAsync(int userId, UserSubscription subscription, DateTime now)
            {
                if (!subscription.AutoRenew || subscription.CancelAtPeriodEnd)
                {
                    return;
                }

                if (subscription.Status == "Active" && now >= subscription.EndDate)
                {
                    subscription.PaymentStatus = "Failed";
                    subscription.Status = "GracePeriod";
                    subscription.GracePeriodEndsAt ??= now.AddDays(GracePeriodDays);
                    subscription.PaymentRetryCount += 1;
                    subscription.LastPaymentRetryAt = now;

                    var key = $"billing-grace-start-{subscription.SubscriptionId}-{subscription.GracePeriodEndsAt:yyyyMMdd}";
                    await NotifyOnceAsync(userId, key, "Payment pending: grace period started",
                        $"We could not process renewal. You are in a {GracePeriodDays}-day grace period until {subscription.GracePeriodEndsAt:dd MMM yyyy}.");
                    await _db.SaveChangesAsync();
                }

                if (subscription.Status == "GracePeriod")
                {
                    if (subscription.GracePeriodEndsAt.HasValue)
                    {
                        var daysLeft = (subscription.GracePeriodEndsAt.Value - now).TotalDays;
                        if (daysLeft <= 2 && daysLeft > 1)
                        {
                            var key2 = $"billing-grace-reminder-2d-{subscription.SubscriptionId}-{subscription.GracePeriodEndsAt:yyyyMMdd}";
                            await NotifyOnceAsync(userId, key2, "2 days left in grace period",
                                "Update your payment method and retry payment to avoid automatic downgrade.");
                        }

                        if (daysLeft <= 1 && daysLeft > 0)
                        {
                            var key1 = $"billing-grace-reminder-1d-{subscription.SubscriptionId}-{subscription.GracePeriodEndsAt:yyyyMMdd}";
                            await NotifyOnceAsync(userId, key1, "Final day before downgrade",
                                "Grace period ends soon. Complete payment retry to keep premium features.");
                        }

                        if (now >= subscription.GracePeriodEndsAt.Value)
                        {
                            await DowngradeToFreeAsync(userId, subscription, now);
                        }
                    }
                }
            }

        private async Task DowngradeToFreeAsync(int userId, UserSubscription subscription, DateTime now)
            {
                var freePlan = await _db.SubscriptionPlans.FirstOrDefaultAsync(p => p.PlanName == "Free");
                if (freePlan == null)
                {
                    return;
                }

                subscription.Status = "Downgraded";
                subscription.PaymentStatus = "Failed";
                subscription.CancelAtPeriodEnd = true;
                subscription.CancelledAt = now;

                var free = new UserSubscription
                {
                    UserId = userId,
                    PlanId = freePlan.PlanId,
                    BillingCycle = "monthly",
                    StartDate = now,
                    EndDate = now.AddMonths(1),
                    Status = "Active",
                    PaymentStatus = "Succeeded",
                    AutoRenew = true,
                    CancelAtPeriodEnd = false,
                    CreatorSearchUsed = 0,
                    CreatorSearchWindowStart = now,
                    CreatedAt = now,
                    GracePeriodEndsAt = null,
                    PaymentRetryCount = 0
                };

                _db.UserSubscriptions.Add(free);

                var key = $"billing-auto-downgraded-{subscription.SubscriptionId}-{now:yyyyMMdd}";
                await NotifyOnceAsync(userId, key, "Subscription downgraded to Free",
                    "Grace period ended without successful payment. We moved you to the Free plan. You can upgrade anytime.");

                await _db.SaveChangesAsync();
            }

        private async Task NotifyOnceAsync(int userId, string type, string title, string message)
            {
                var exists = await _db.UserNotifications
                    .AsNoTracking()
                    .AnyAsync(x => x.UserId == userId && x.Type == type);

                if (exists)
                {
                    return;
                }

                await _notifications.NotifyAsync(new NotificationCreateRequestDto
                {
                    UserId = userId,
                    Type = type,
                    Title = title,
                    Message = message,
                    SendEmail = true
                });
            }

        private static DateTime NextCycleEnd(DateTime from, string billingCycle)
            {
                return string.Equals(billingCycle, "yearly", StringComparison.OrdinalIgnoreCase)
                    ? from.AddYears(1)
                    : from.AddMonths(1);
            }

        private async Task EmitLifecycleNotificationsAsync(int userId, UserSubscription subscription)
        {
            var now = DateTime.UtcNow;
            var daysLeft = (subscription.EndDate - now).TotalDays;

            if (daysLeft <= 7 && daysLeft > 0)
            {
                var key = $"renewal-reminder-{subscription.SubscriptionId}-{subscription.EndDate:yyyyMMdd}";
                if (!await _db.UserNotifications.AnyAsync(x => x.UserId == userId && x.Type == key))
                {
                    await _notifications.NotifyAsync(new NotificationCreateRequestDto
                    {
                        UserId = userId,
                        Type = key,
                        Title = "Renewal reminder",
                        Message = $"Your plan renews on {subscription.EndDate:dd MMM yyyy}.",
                        SendEmail = true
                    });
                }
            }

            if (daysLeft <= 0)
            {
                var key = $"plan-expiry-{subscription.SubscriptionId}-{subscription.EndDate:yyyyMMdd}";
                if (!await _db.UserNotifications.AnyAsync(x => x.UserId == userId && x.Type == key))
                {
                    await _notifications.NotifyAsync(new NotificationCreateRequestDto
                    {
                        UserId = userId,
                        Type = key,
                        Title = "Plan expired",
                        Message = "Your subscription period has ended. Reactivate to continue premium access.",
                        SendEmail = true
                    });
                }
            }
        }
    }
}
