using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using InfluencerMatch.Application.DTOs;
using InfluencerMatch.Application.Interfaces;
using Microsoft.Extensions.Options;

namespace InfluencerMatch.Infrastructure.Services.PaymentProviders
{
    public class RazorpayPaymentProviderClient : IPaymentProviderClient
    {
        private readonly IHttpClientFactory _httpFactory;
        private readonly PaymentProviderOptions _options;

        public RazorpayPaymentProviderClient(IHttpClientFactory httpFactory, IOptions<PaymentProviderOptions> options)
        {
            _httpFactory = httpFactory;
            _options = options.Value;
        }

        public string ProviderName => "razorpay";

        public async Task<PaymentInitResultDto> CreatePaymentIntentAsync(decimal amount, string currency, string description)
        {
            var cfg = _options.Razorpay;
            if (!cfg.Enabled || string.IsNullOrWhiteSpace(cfg.ApiKey) || string.IsNullOrWhiteSpace(cfg.Secret))
            {
                return new PaymentInitResultDto
                {
                    Provider = ProviderName,
                    PaymentStatus = amount == 0 ? "Succeeded" : "Pending",
                    ProviderPaymentId = $"razorpay_test_{Guid.NewGuid():N}",
                    Message = "Razorpay is in sandbox hook mode. Configure PaymentProviders:Razorpay to enable live API calls."
                };
            }

            var client = _httpFactory.CreateClient();
            client.BaseAddress = new Uri(cfg.BaseUrl ?? "https://api.razorpay.com");

            var basicToken = Convert.ToBase64String(Encoding.UTF8.GetBytes($"{cfg.ApiKey}:{cfg.Secret}"));
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Basic", basicToken);

            var amountInMinorUnits = (long)Math.Round(amount * 100, MidpointRounding.AwayFromZero);
            var payload = new
            {
                amount = amountInMinorUnits,
                currency = currency.ToUpperInvariant(),
                receipt = $"sub_{Guid.NewGuid():N}",
                notes = new { description }
            };

            var response = await client.PostAsJsonAsync("/v1/orders", payload);
            if (!response.IsSuccessStatusCode)
            {
                var body = await response.Content.ReadAsStringAsync();
                throw new InvalidOperationException($"Razorpay order init failed: {response.StatusCode} {body}");
            }

            var data = await response.Content.ReadFromJsonAsync<RazorpayOrderResponse>();
            return new PaymentInitResultDto
            {
                Provider = ProviderName,
                PaymentStatus = amount == 0 ? "Succeeded" : "Pending",
                ProviderPaymentId = data?.id,
                Message = "Razorpay order created."
            };
        }

        private sealed class RazorpayOrderResponse
        {
            public string? id { get; set; }
        }
    }
}
