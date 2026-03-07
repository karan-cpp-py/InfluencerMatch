using System.ComponentModel.DataAnnotations;

namespace InfluencerMatch.Domain.Entities
{
    public class Invoice
    {
        [Key]
        public int InvoiceId { get; set; }

        public int UserId { get; set; }

        public int SubscriptionId { get; set; }

        public decimal Amount { get; set; }

        [Required]
        public string Currency { get; set; } = "INR";

        [Required]
        public string Status { get; set; } = "Pending";

        [Required]
        public string Provider { get; set; } = string.Empty;

        public string? ProviderInvoiceId { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        public User? User { get; set; }

        public UserSubscription? Subscription { get; set; }
    }
}
