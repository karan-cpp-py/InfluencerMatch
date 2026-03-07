using System.Net.Http.Headers;
using System.Net.Http.Json;
using InfluencerMatch.Application.DTOs;
using InfluencerMatch.Application.Interfaces;
using Microsoft.Extensions.Options;

namespace InfluencerMatch.Infrastructure.Services.PaymentProviders
{
    public class PayPalPaymentProviderClient : IPaymentProviderClient
    {
        private readonly IHttpClientFactory _httpFactory;
        private readonly PaymentProviderOptions _options;

        public PayPalPaymentProviderClient(IHttpClientFactory httpFactory, IOptions<PaymentProviderOptions> options)
        {
            _httpFactory = httpFactory;
            _options = options.Value;
        }

        public string ProviderName => "paypal";

        public async Task<PaymentInitResultDto> CreatePaymentIntentAsync(decimal amount, string currency, string description)
        {
            var cfg = _options.PayPal;
            if (!cfg.Enabled || string.IsNullOrWhiteSpace(cfg.ApiKey) || string.IsNullOrWhiteSpace(cfg.Secret))
            {
                return new PaymentInitResultDto
                {
                    Provider = ProviderName,
                    PaymentStatus = amount == 0 ? "Succeeded" : "Pending",
                    ProviderPaymentId = $"paypal_test_{Guid.NewGuid():N}",
                    Message = "PayPal is in sandbox hook mode. Configure PaymentProviders:PayPal to enable live API calls."
                };
            }

            var client = _httpFactory.CreateClient();
            client.BaseAddress = new Uri(cfg.BaseUrl ?? "https://api-m.sandbox.paypal.com");

            var accessToken = await GetAccessTokenAsync(client, cfg.ApiKey, cfg.Secret);
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);

            var payload = new
            {
                intent = "CAPTURE",
                purchase_units = new[]
                {
                    new
                    {
                        description,
                        amount = new
                        {
                            currency_code = currency.ToUpperInvariant(),
                            value = amount.ToString("0.00", System.Globalization.CultureInfo.InvariantCulture)
                        }
                    }
                }
            };

            var response = await client.PostAsJsonAsync("/v2/checkout/orders", payload);
            if (!response.IsSuccessStatusCode)
            {
                var body = await response.Content.ReadAsStringAsync();
                throw new InvalidOperationException($"PayPal order init failed: {response.StatusCode} {body}");
            }

            var data = await response.Content.ReadFromJsonAsync<PayPalOrderResponse>();
            return new PaymentInitResultDto
            {
                Provider = ProviderName,
                PaymentStatus = amount == 0 ? "Succeeded" : "Pending",
                ProviderPaymentId = data?.id,
                Message = "PayPal order created."
            };
        }

        private static async Task<string> GetAccessTokenAsync(HttpClient client, string clientId, string secret)
        {
            var basicToken = Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes($"{clientId}:{secret}"));
            using var request = new HttpRequestMessage(HttpMethod.Post, "/v1/oauth2/token");
            request.Headers.Authorization = new AuthenticationHeaderValue("Basic", basicToken);
            request.Content = new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["grant_type"] = "client_credentials"
            });

            var tokenResponse = await client.SendAsync(request);
            if (!tokenResponse.IsSuccessStatusCode)
            {
                var body = await tokenResponse.Content.ReadAsStringAsync();
                throw new InvalidOperationException($"PayPal token fetch failed: {tokenResponse.StatusCode} {body}");
            }

            var data = await tokenResponse.Content.ReadFromJsonAsync<PayPalTokenResponse>();
            if (string.IsNullOrWhiteSpace(data?.access_token))
            {
                throw new InvalidOperationException("PayPal token response did not include an access token.");
            }

            return data.access_token;
        }

        private sealed class PayPalTokenResponse
        {
            public string? access_token { get; set; }
        }

        private sealed class PayPalOrderResponse
        {
            public string? id { get; set; }
        }
    }
}
