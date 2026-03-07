using System.ComponentModel.DataAnnotations;

namespace InfluencerMatch.Domain.Entities
{
    public class UserSubscription
    {
        [Key]
        public int SubscriptionId { get; set; }

        public int UserId { get; set; }

        public int PlanId { get; set; }

        [Required]
        public string BillingCycle { get; set; } = "monthly";

        public DateTime StartDate { get; set; }

        public DateTime EndDate { get; set; }

        [Required]
        public string Status { get; set; } = "Active";

        [Required]
        public string PaymentStatus { get; set; } = "Pending";

        public bool AutoRenew { get; set; } = true;

        public bool CancelAtPeriodEnd { get; set; }

        public DateTime? CancelledAt { get; set; }

        public string? PaymentMethodBrand { get; set; }

        public string? PaymentMethodLast4 { get; set; }

        public DateTime? GracePeriodEndsAt { get; set; }

        public int PaymentRetryCount { get; set; }

        public DateTime? LastPaymentRetryAt { get; set; }

        // Tracks monthly usage for plan-based search limits.
        public int CreatorSearchUsed { get; set; }

        public DateTime CreatorSearchWindowStart { get; set; } = DateTime.UtcNow;

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        public User? User { get; set; }

        public SubscriptionPlan? Plan { get; set; }

        public ICollection<PaymentRecord> Payments { get; set; } = new List<PaymentRecord>();
    }
}
