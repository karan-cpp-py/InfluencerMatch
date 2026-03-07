using InfluencerMatch.Application.DTOs;
using InfluencerMatch.Application.Interfaces;
using Microsoft.Extensions.DependencyInjection;

namespace InfluencerMatch.Infrastructure.Services
{
    public class PaymentGatewayService : IPaymentGatewayService
    {
        private readonly IReadOnlyDictionary<string, IPaymentProviderClient> _providerClients;

        public PaymentGatewayService(IEnumerable<IPaymentProviderClient> providerClients)
        {
            _providerClients = providerClients
                .ToDictionary(p => p.ProviderName, StringComparer.OrdinalIgnoreCase);
        }

        public Task<PaymentInitResultDto> InitializePaymentAsync(
            string provider,
            decimal amount,
            string currency,
            string description)
        {
            var normalizedProvider = provider.Trim();
            if (!_providerClients.TryGetValue(normalizedProvider, out var client))
            {
                throw new InvalidOperationException("Unsupported payment provider. Use stripe, razorpay, or paypal.");
            }

            return client.CreatePaymentIntentAsync(amount, currency, description);
        }
    }
}
