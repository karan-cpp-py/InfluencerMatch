using System.Net.Http.Headers;
using System.Net.Http.Json;
using InfluencerMatch.Application.DTOs;
using InfluencerMatch.Application.Interfaces;
using Microsoft.Extensions.Options;

namespace InfluencerMatch.Infrastructure.Services.PaymentProviders
{
    public class StripePaymentProviderClient : IPaymentProviderClient
    {
        private readonly IHttpClientFactory _httpFactory;
        private readonly PaymentProviderOptions _options;

        public StripePaymentProviderClient(IHttpClientFactory httpFactory, IOptions<PaymentProviderOptions> options)
        {
            _httpFactory = httpFactory;
            _options = options.Value;
        }

        public string ProviderName => "stripe";

        public async Task<PaymentInitResultDto> CreatePaymentIntentAsync(decimal amount, string currency, string description)
        {
            var cfg = _options.Stripe;
            if (!cfg.Enabled || string.IsNullOrWhiteSpace(cfg.ApiKey))
            {
                return new PaymentInitResultDto
                {
                    Provider = ProviderName,
                    PaymentStatus = amount == 0 ? "Succeeded" : "Pending",
                    ProviderPaymentId = $"stripe_test_{Guid.NewGuid():N}",
                    Message = "Stripe is in sandbox hook mode. Configure PaymentProviders:Stripe to enable live API calls."
                };
            }

            var client = _httpFactory.CreateClient();
            client.BaseAddress = new Uri(cfg.BaseUrl ?? "https://api.stripe.com");
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", cfg.ApiKey);

            var amountInMinorUnits = (long)Math.Round(amount * 100, MidpointRounding.AwayFromZero);
            var formData = new Dictionary<string, string>
            {
                ["amount"] = amountInMinorUnits.ToString(),
                ["currency"] = currency.ToLowerInvariant(),
                ["description"] = description,
                ["automatic_payment_methods[enabled]"] = "true"
            };

            var response = await client.PostAsync("/v1/payment_intents", new FormUrlEncodedContent(formData));
            if (!response.IsSuccessStatusCode)
            {
                var payload = await response.Content.ReadAsStringAsync();
                throw new InvalidOperationException($"Stripe payment init failed: {response.StatusCode} {payload}");
            }

            var data = await response.Content.ReadFromJsonAsync<StripeIntentResponse>();
            return new PaymentInitResultDto
            {
                Provider = ProviderName,
                PaymentStatus = amount == 0 ? "Succeeded" : "Pending",
                ProviderPaymentId = data?.id,
                Message = "Stripe payment intent created."
            };
        }

        private sealed class StripeIntentResponse
        {
            public string? id { get; set; }
        }
    }
}
