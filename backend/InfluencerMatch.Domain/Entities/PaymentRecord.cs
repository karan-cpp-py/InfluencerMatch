using System.ComponentModel.DataAnnotations;

namespace InfluencerMatch.Domain.Entities
{
    public class PaymentRecord
    {
        [Key]
        public int PaymentRecordId { get; set; }

        public int SubscriptionId { get; set; }

        [Required]
        public string Provider { get; set; } = string.Empty;

        public decimal Amount { get; set; }

        [Required]
        public string Currency { get; set; } = "INR";

        [Required]
        public string PaymentStatus { get; set; } = "Pending";

        public string? ProviderPaymentId { get; set; }

        public string? ProviderEventId { get; set; }

        public string? ProviderRawPayload { get; set; }

        public DateTime? UpdatedAt { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        public UserSubscription? Subscription { get; set; }
    }
}
