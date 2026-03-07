using InfluencerMatch.Application.DTOs;

namespace InfluencerMatch.Application.Interfaces
{
    public interface IPaymentProviderClient
    {
        string ProviderName { get; }

        Task<PaymentInitResultDto> CreatePaymentIntentAsync(decimal amount, string currency, string description);
    }
}
