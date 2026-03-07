using System.ComponentModel.DataAnnotations;

namespace InfluencerMatch.Domain.Entities
{
    public class SubscriptionPlan
    {
        [Key]
        public int PlanId { get; set; }

        [Required]
        public string PlanName { get; set; } = string.Empty;

        public decimal PriceMonthly { get; set; }

        public decimal PriceYearly { get; set; }

        // Null means unlimited.
        public int? MaxCreatorSearch { get; set; }

        public bool ExportAllowed { get; set; }

        // Basic, Advanced, Custom
        public string AnalyticsAccessLevel { get; set; } = "Basic";

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        public ICollection<UserSubscription> UserSubscriptions { get; set; } = new List<UserSubscription>();
    }
}
