using InfluencerMatch.Application.DTOs;

namespace InfluencerMatch.Application.Interfaces
{
    public interface IPaymentGatewayService
    {
        Task<PaymentInitResultDto> InitializePaymentAsync(string provider, decimal amount, string currency, string description);
    }
}
