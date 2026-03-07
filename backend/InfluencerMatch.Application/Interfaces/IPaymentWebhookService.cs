namespace InfluencerMatch.Application.Interfaces
{
    public interface IPaymentWebhookService
    {
        Task<bool> ProcessWebhookAsync(string provider, string payload, IReadOnlyDictionary<string, string> headers);
    }
}
