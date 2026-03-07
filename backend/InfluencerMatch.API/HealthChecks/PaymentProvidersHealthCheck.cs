using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using InfluencerMatch.Infrastructure.Services.PaymentProviders;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Options;

namespace InfluencerMatch.API.HealthChecks
{
    public class PaymentProvidersHealthCheck : IHealthCheck
    {
        private readonly IHttpClientFactory _httpClientFactory;
        private readonly PaymentProviderOptions _options;

        public PaymentProvidersHealthCheck(IHttpClientFactory httpClientFactory, IOptions<PaymentProviderOptions> options)
        {
            _httpClientFactory = httpClientFactory;
            _options = options.Value;
        }

        public async Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default)
        {
            var checks = new List<string>();
            var failures = new List<string>();

            await CheckProviderAsync("stripe", _options.Stripe.BaseUrl, _options.Stripe.Enabled, checks, failures, cancellationToken);
            await CheckProviderAsync("razorpay", _options.Razorpay.BaseUrl, _options.Razorpay.Enabled, checks, failures, cancellationToken);
            await CheckProviderAsync("paypal", _options.PayPal.BaseUrl, _options.PayPal.Enabled, checks, failures, cancellationToken);

            if (failures.Count > 0)
            {
                return HealthCheckResult.Degraded("Some payment provider health checks failed.", data: new Dictionary<string, object>
                {
                    ["checks"] = string.Join(", ", checks),
                    ["failures"] = string.Join(" | ", failures)
                });
            }

            return HealthCheckResult.Healthy("Payment providers reachable.", new Dictionary<string, object>
            {
                ["checks"] = string.Join(", ", checks)
            });
        }

        private async Task CheckProviderAsync(
            string provider,
            string? baseUrl,
            bool enabled,
            List<string> checks,
            List<string> failures,
            CancellationToken cancellationToken)
        {
            if (!enabled)
            {
                checks.Add($"{provider}=disabled");
                return;
            }

            if (string.IsNullOrWhiteSpace(baseUrl))
            {
                failures.Add($"{provider}: base URL missing");
                return;
            }

            try
            {
                var client = _httpClientFactory.CreateClient();
                client.Timeout = TimeSpan.FromSeconds(5);
                using var req = new HttpRequestMessage(HttpMethod.Get, baseUrl);
                using var res = await client.SendAsync(req, cancellationToken);
                checks.Add($"{provider}={(int)res.StatusCode}");
                if ((int)res.StatusCode >= 500)
                {
                    failures.Add($"{provider}: upstream status {(int)res.StatusCode}");
                }
            }
            catch (Exception ex)
            {
                failures.Add($"{provider}: {ex.GetType().Name}");
            }
        }
    }
}
