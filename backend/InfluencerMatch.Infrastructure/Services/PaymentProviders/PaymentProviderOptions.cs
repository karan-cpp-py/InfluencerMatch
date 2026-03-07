namespace InfluencerMatch.Infrastructure.Services.PaymentProviders
{
    public class PaymentProviderOptions
    {
        public ProviderConfig Stripe { get; set; } = new();
        public ProviderConfig Razorpay { get; set; } = new();
        public ProviderConfig PayPal { get; set; } = new();
    }

    public class ProviderConfig
    {
        public bool Enabled { get; set; }
        public string? ApiKey { get; set; }
        public string? Secret { get; set; }
        public string? BaseUrl { get; set; }
        public string? WebhookSecret { get; set; }
        public string? WebhookId { get; set; }
        public int WebhookReplayToleranceMinutes { get; set; } = 10;
    }
}
