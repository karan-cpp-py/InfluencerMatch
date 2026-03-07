using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using InfluencerMatch.Application.Interfaces;
using InfluencerMatch.Infrastructure.Data;
using InfluencerMatch.Infrastructure.Services.PaymentProviders;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace InfluencerMatch.Infrastructure.Services
{
    public class PaymentWebhookService : IPaymentWebhookService
    {
        private readonly ApplicationDbContext _db;
        private readonly PaymentProviderOptions _options;
        private readonly IHttpClientFactory _httpClientFactory;
        private readonly ILogger<PaymentWebhookService> _logger;
        private readonly INotificationService _notifications;

        public PaymentWebhookService(
            ApplicationDbContext db,
            IOptions<PaymentProviderOptions> options,
            IHttpClientFactory httpClientFactory,
            ILogger<PaymentWebhookService> logger,
            INotificationService notifications)
        {
            _db = db;
            _options = options.Value;
            _httpClientFactory = httpClientFactory;
            _logger = logger;
            _notifications = notifications;
        }

        public async Task<bool> ProcessWebhookAsync(string provider, string payload, IReadOnlyDictionary<string, string> headers)
        {
            var normalizedProvider = provider.Trim().ToLowerInvariant();
            if (normalizedProvider is not ("stripe" or "razorpay" or "paypal"))
            {
                throw new InvalidOperationException("Unsupported payment provider.");
            }

            var verificationResult = await VerifyWebhookAsync(normalizedProvider, payload, headers);
            if (!verificationResult.Verified)
            {
                _logger.LogWarning(
                    "Webhook verification failed for provider={Provider}, reason={Reason}",
                    normalizedProvider,
                    verificationResult.FailureReason);
                throw new UnauthorizedAccessException("Webhook signature verification failed.");
            }

            if (!string.IsNullOrWhiteSpace(verificationResult.ReplayKey))
            {
                var replayExists = await _db.WebhookEvents.AnyAsync(x =>
                    x.Provider == normalizedProvider && x.EventId == verificationResult.ReplayKey);
                if (replayExists)
                {
                    _logger.LogWarning(
                        "Replay webhook blocked for provider={Provider}, replayKey={ReplayKey}",
                        normalizedProvider,
                        verificationResult.ReplayKey);
                    throw new UnauthorizedAccessException("Replayed webhook request detected.");
                }
            }

            var parse = ParseWebhook(normalizedProvider, payload);

            if (!string.IsNullOrWhiteSpace(parse.ProviderEventId))
            {
                var eventReplay = await _db.WebhookEvents.AnyAsync(x =>
                    x.Provider == normalizedProvider && x.EventId == parse.ProviderEventId);
                if (eventReplay)
                {
                    _logger.LogWarning(
                        "Replay webhook blocked by provider event id provider={Provider} eventId={EventId}",
                        normalizedProvider,
                        parse.ProviderEventId);
                    throw new UnauthorizedAccessException("Replayed webhook request detected.");
                }
            }

            if (string.IsNullOrWhiteSpace(parse.ProviderPaymentId) && string.IsNullOrWhiteSpace(parse.FallbackPaymentId))
            {
                return false;
            }

            var paymentRecord = await FindPaymentRecordAsync(normalizedProvider, parse.ProviderPaymentId, parse.FallbackPaymentId);
            if (paymentRecord == null)
            {
                return false;
            }

            paymentRecord.ProviderEventId = parse.ProviderEventId;
            paymentRecord.ProviderRawPayload = payload;
            paymentRecord.PaymentStatus = parse.PaymentStatus;
            paymentRecord.UpdatedAt = DateTime.UtcNow;

            var webhookEventId = !string.IsNullOrWhiteSpace(parse.ProviderEventId)
                ? parse.ProviderEventId
                : verificationResult.ReplayKey;

            if (!string.IsNullOrWhiteSpace(webhookEventId))
            {
                _db.WebhookEvents.Add(new Domain.Entities.WebhookEvent
                {
                    Provider = normalizedProvider,
                    EventId = webhookEventId,
                    PayloadHash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(payload))),
                    Status = "Processed",
                    CreatedAt = DateTime.UtcNow
                });
            }

            await _db.Entry(paymentRecord).Reference(p => p.Subscription).LoadAsync();
            var subscription = paymentRecord.Subscription;
            if (subscription != null)
            {
                subscription.PaymentStatus = parse.PaymentStatus;

                if (parse.PaymentStatus == "Succeeded")
                {
                    subscription.Status = "Active";
                }
                else if (parse.PaymentStatus == "Failed")
                {
                    subscription.Status = "PaymentFailed";
                }

                await _notifications.NotifyAsync(new Application.DTOs.NotificationCreateRequestDto
                {
                    UserId = subscription.UserId,
                    Type = parse.PaymentStatus == "Succeeded" ? "payment.succeeded" : parse.PaymentStatus == "Failed" ? "payment.failed" : "payment.updated",
                    Title = parse.PaymentStatus == "Succeeded" ? "Payment successful" : parse.PaymentStatus == "Failed" ? "Payment failed" : "Payment update",
                    Message = parse.PaymentStatus == "Succeeded"
                        ? "Your payment has been confirmed and subscription remains active."
                        : parse.PaymentStatus == "Failed"
                            ? "Your payment failed. Update your payment method to avoid interruption."
                            : $"Payment status updated: {parse.PaymentStatus}",
                    SendEmail = true
                });
            }

            var invoice = await _db.Invoices
                .Where(i => i.SubscriptionId == paymentRecord.SubscriptionId)
                .OrderByDescending(i => i.CreatedAt)
                .FirstOrDefaultAsync();
            if (invoice != null)
            {
                invoice.Status = parse.PaymentStatus;
                if (string.IsNullOrWhiteSpace(invoice.ProviderInvoiceId))
                {
                    invoice.ProviderInvoiceId = paymentRecord.ProviderPaymentId;
                }
            }

            await _db.SaveChangesAsync();
            return true;
        }

        private async Task<WebhookVerificationResult> VerifyWebhookAsync(
            string provider,
            string payload,
            IReadOnlyDictionary<string, string> headers)
        {
            try
            {
                return provider switch
                {
                    "stripe" => VerifyStripe(payload, headers),
                    "razorpay" => VerifyRazorpay(payload, headers),
                    "paypal" => await VerifyPayPalAsync(payload, headers),
                    _ => WebhookVerificationResult.Fail("Unsupported provider.")
                };
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Webhook signature verification exception for provider={Provider}", provider);
                return WebhookVerificationResult.Fail("Verification exception.");
            }
        }

        private WebhookVerificationResult VerifyStripe(string payload, IReadOnlyDictionary<string, string> headers)
        {
            if (!headers.TryGetValue("Stripe-Signature", out var signatureHeader) || string.IsNullOrWhiteSpace(signatureHeader))
            {
                return WebhookVerificationResult.Fail("Missing Stripe-Signature header.");
            }

            var cfg = _options.Stripe;
            if (string.IsNullOrWhiteSpace(cfg.WebhookSecret))
            {
                return WebhookVerificationResult.Fail("Stripe webhook secret not configured.");
            }

            var parts = signatureHeader.Split(',', StringSplitOptions.RemoveEmptyEntries)
                .Select(x => x.Split('=', 2))
                .Where(x => x.Length == 2)
                .ToDictionary(x => x[0].Trim(), x => x[1].Trim(), StringComparer.OrdinalIgnoreCase);

            if (!parts.TryGetValue("t", out var timestampRaw) || !parts.TryGetValue("v1", out var v1Sig))
            {
                return WebhookVerificationResult.Fail("Invalid Stripe signature payload.");
            }

            if (!long.TryParse(timestampRaw, out var unix))
            {
                return WebhookVerificationResult.Fail("Invalid Stripe timestamp.");
            }

            var requestTime = DateTimeOffset.FromUnixTimeSeconds(unix);
            var tolerance = TimeSpan.FromMinutes(cfg.WebhookReplayToleranceMinutes <= 0 ? 10 : cfg.WebhookReplayToleranceMinutes);
            if (DateTimeOffset.UtcNow - requestTime > tolerance)
            {
                return WebhookVerificationResult.Fail("Stripe request outside replay tolerance window.");
            }

            var signedPayload = $"{timestampRaw}.{payload}";
            using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(cfg.WebhookSecret));
            var computed = Convert.ToHexString(hmac.ComputeHash(Encoding.UTF8.GetBytes(signedPayload))).ToLowerInvariant();
            if (!ConstantTimeEquals(computed, v1Sig.ToLowerInvariant()))
            {
                return WebhookVerificationResult.Fail("Stripe signature mismatch.");
            }

            return WebhookVerificationResult.Ok($"stripe-{timestampRaw}-{v1Sig}");
        }

        private WebhookVerificationResult VerifyRazorpay(string payload, IReadOnlyDictionary<string, string> headers)
        {
            if (!headers.TryGetValue("X-Razorpay-Signature", out var signature) || string.IsNullOrWhiteSpace(signature))
            {
                return WebhookVerificationResult.Fail("Missing X-Razorpay-Signature header.");
            }

            var cfg = _options.Razorpay;
            if (string.IsNullOrWhiteSpace(cfg.WebhookSecret))
            {
                return WebhookVerificationResult.Fail("Razorpay webhook secret not configured.");
            }

            using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(cfg.WebhookSecret));
            var computed = Convert.ToHexString(hmac.ComputeHash(Encoding.UTF8.GetBytes(payload))).ToLowerInvariant();
            if (!ConstantTimeEquals(computed, signature.ToLowerInvariant()))
            {
                return WebhookVerificationResult.Fail("Razorpay signature mismatch.");
            }

            headers.TryGetValue("X-Razorpay-Event-Id", out var eventId);
            var replayKey = string.IsNullOrWhiteSpace(eventId)
                ? $"razorpay-{signature}"
                : $"razorpay-{eventId}";

            return WebhookVerificationResult.Ok(replayKey);
        }

        private async Task<WebhookVerificationResult> VerifyPayPalAsync(string payload, IReadOnlyDictionary<string, string> headers)
        {
            var requiredHeaders = new[]
            {
                "Paypal-Transmission-Id",
                "Paypal-Transmission-Time",
                "Paypal-Transmission-Sig",
                "Paypal-Cert-Url",
                "Paypal-Auth-Algo"
            };

            foreach (var h in requiredHeaders)
            {
                if (!headers.TryGetValue(h, out var val) || string.IsNullOrWhiteSpace(val))
                {
                    return WebhookVerificationResult.Fail($"Missing {h} header.");
                }
            }

            if (!headers.TryGetValue("Paypal-Transmission-Id", out var transmissionId))
            {
                return WebhookVerificationResult.Fail("Missing PayPal transmission ID.");
            }

            var cfg = _options.PayPal;
            if (string.IsNullOrWhiteSpace(cfg.ApiKey)
                || string.IsNullOrWhiteSpace(cfg.Secret)
                || string.IsNullOrWhiteSpace(cfg.WebhookId))
            {
                return WebhookVerificationResult.Fail("PayPal webhook verification config missing.");
            }

            var client = _httpClientFactory.CreateClient();
            client.BaseAddress = new Uri(cfg.BaseUrl ?? "https://api-m.sandbox.paypal.com");
            var accessToken = await GetPayPalAccessTokenAsync(client, cfg.ApiKey, cfg.Secret);
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);

            var verifyReq = new
            {
                auth_algo = headers["Paypal-Auth-Algo"],
                cert_url = headers["Paypal-Cert-Url"],
                transmission_id = headers["Paypal-Transmission-Id"],
                transmission_sig = headers["Paypal-Transmission-Sig"],
                transmission_time = headers["Paypal-Transmission-Time"],
                webhook_id = cfg.WebhookId,
                webhook_event = JsonDocument.Parse(payload).RootElement
            };

            var verifyRes = await client.PostAsJsonAsync("/v1/notifications/verify-webhook-signature", verifyReq);
            if (!verifyRes.IsSuccessStatusCode)
            {
                return WebhookVerificationResult.Fail("PayPal signature verification API failed.");
            }

            using var doc = JsonDocument.Parse(await verifyRes.Content.ReadAsStringAsync());
            var verificationStatus = doc.RootElement.TryGetProperty("verification_status", out var statusNode)
                ? statusNode.GetString()
                : null;

            if (!string.Equals(verificationStatus, "SUCCESS", StringComparison.OrdinalIgnoreCase))
            {
                return WebhookVerificationResult.Fail("PayPal verification status is not SUCCESS.");
            }

            return WebhookVerificationResult.Ok($"paypal-{transmissionId}");
        }

        private static async Task<string> GetPayPalAccessTokenAsync(HttpClient client, string apiKey, string secret)
        {
            var basic = Convert.ToBase64String(Encoding.UTF8.GetBytes($"{apiKey}:{secret}"));
            using var request = new HttpRequestMessage(HttpMethod.Post, "/v1/oauth2/token");
            request.Headers.Authorization = new AuthenticationHeaderValue("Basic", basic);
            request.Content = new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["grant_type"] = "client_credentials"
            });

            var response = await client.SendAsync(request);
            response.EnsureSuccessStatusCode();
            var tokenPayload = await response.Content.ReadFromJsonAsync<PayPalTokenResponse>();
            if (string.IsNullOrWhiteSpace(tokenPayload?.access_token))
            {
                throw new InvalidOperationException("PayPal token response missing access token.");
            }

            return tokenPayload.access_token;
        }

        private static bool ConstantTimeEquals(string a, string b)
        {
            if (a.Length != b.Length)
            {
                return false;
            }

            var diff = 0;
            for (var i = 0; i < a.Length; i++)
            {
                diff |= a[i] ^ b[i];
            }

            return diff == 0;
        }

        private async Task<Domain.Entities.PaymentRecord?> FindPaymentRecordAsync(
            string provider,
            string? providerPaymentId,
            string? fallbackPaymentId)
        {
            var query = _db.PaymentRecords.Where(p => p.Provider == provider);

            if (!string.IsNullOrWhiteSpace(providerPaymentId))
            {
                var direct = await query.FirstOrDefaultAsync(p => p.ProviderPaymentId == providerPaymentId);
                if (direct != null)
                {
                    return direct;
                }
            }

            if (!string.IsNullOrWhiteSpace(fallbackPaymentId))
            {
                var fallback = await query.FirstOrDefaultAsync(p => p.ProviderPaymentId == fallbackPaymentId);
                if (fallback != null)
                {
                    return fallback;
                }
            }

            return null;
        }

        private ParsedWebhook ParseWebhook(string provider, string payload)
        {
            using var doc = JsonDocument.Parse(payload);

            return provider switch
            {
                "stripe" => ParseStripeWebhook(doc.RootElement),
                "razorpay" => ParseRazorpayWebhook(doc.RootElement),
                "paypal" => ParsePayPalWebhook(doc.RootElement),
                _ => throw new InvalidOperationException("Unsupported payment provider.")
            };
        }

        private static ParsedWebhook ParseStripeWebhook(JsonElement root)
        {
            var eventId = root.TryGetProperty("id", out var idNode) ? idNode.GetString() : null;
            var eventType = root.TryGetProperty("type", out var typeNode) ? typeNode.GetString() : null;

            var obj = root
                .GetProperty("data")
                .GetProperty("object");

            var paymentIntentId = obj.TryGetProperty("id", out var piNode) ? piNode.GetString() : null;
            return new ParsedWebhook
            {
                ProviderEventId = eventId,
                ProviderPaymentId = paymentIntentId,
                PaymentStatus = eventType switch
                {
                    "payment_intent.succeeded" => "Succeeded",
                    "payment_intent.payment_failed" => "Failed",
                    _ => "Pending"
                }
            };
        }

        private static ParsedWebhook ParseRazorpayWebhook(JsonElement root)
        {
            var eventId = root.TryGetProperty("payload", out var payloadNode)
                          && payloadNode.TryGetProperty("payment", out var paymentNode)
                          && paymentNode.TryGetProperty("entity", out var entityNode)
                          && entityNode.TryGetProperty("id", out var paymentIdNode)
                ? paymentIdNode.GetString()
                : null;

            var eventType = root.TryGetProperty("event", out var eventTypeNode)
                ? eventTypeNode.GetString()
                : null;

            string? orderId = null;
            if (root.TryGetProperty("payload", out var payload)
                && payload.TryGetProperty("payment", out var pNode)
                && pNode.TryGetProperty("entity", out var eNode)
                && eNode.TryGetProperty("order_id", out var orderIdNode))
            {
                orderId = orderIdNode.GetString();
            }

            return new ParsedWebhook
            {
                ProviderEventId = eventId,
                ProviderPaymentId = orderId,
                FallbackPaymentId = eventId,
                PaymentStatus = eventType switch
                {
                    "payment.captured" => "Succeeded",
                    "order.paid" => "Succeeded",
                    "payment.failed" => "Failed",
                    _ => "Pending"
                }
            };
        }

        private static ParsedWebhook ParsePayPalWebhook(JsonElement root)
        {
            var eventId = root.TryGetProperty("id", out var eventIdNode) ? eventIdNode.GetString() : null;
            var eventType = root.TryGetProperty("event_type", out var eventTypeNode) ? eventTypeNode.GetString() : null;

            string? resourceId = null;
            if (root.TryGetProperty("resource", out var resource))
            {
                if (resource.TryGetProperty("id", out var rid))
                {
                    resourceId = rid.GetString();
                }
                else if (resource.TryGetProperty("supplementary_data", out var supp)
                         && supp.TryGetProperty("related_ids", out var related)
                         && related.TryGetProperty("order_id", out var orderIdNode))
                {
                    resourceId = orderIdNode.GetString();
                }
            }

            return new ParsedWebhook
            {
                ProviderEventId = eventId,
                ProviderPaymentId = resourceId,
                PaymentStatus = eventType switch
                {
                    "PAYMENT.CAPTURE.COMPLETED" => "Succeeded",
                    "CHECKOUT.ORDER.APPROVED" => "Succeeded",
                    "PAYMENT.CAPTURE.DENIED" => "Failed",
                    "PAYMENT.CAPTURE.DECLINED" => "Failed",
                    _ => "Pending"
                }
            };
        }

        private sealed class ParsedWebhook
        {
            public string? ProviderEventId { get; set; }
            public string? ProviderPaymentId { get; set; }
            public string? FallbackPaymentId { get; set; }
            public string PaymentStatus { get; set; } = "Pending";
        }

        private sealed class WebhookVerificationResult
        {
            public bool Verified { get; init; }
            public string? ReplayKey { get; init; }
            public string? FailureReason { get; init; }

            public static WebhookVerificationResult Ok(string? replayKey) => new()
            {
                Verified = true,
                ReplayKey = replayKey
            };

            public static WebhookVerificationResult Fail(string reason) => new()
            {
                Verified = false,
                FailureReason = reason
            };
        }

        private sealed class PayPalTokenResponse
        {
            public string? access_token { get; set; }
        }
    }
}
