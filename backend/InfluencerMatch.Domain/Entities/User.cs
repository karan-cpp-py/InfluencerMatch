using System.ComponentModel.DataAnnotations;

namespace InfluencerMatch.Domain.Entities
{
    public class User
    {
        [Key]
        public int UserId { get; set; }

        [Required]
        public string Name { get; set; }

        [Required]
        public string Email { get; set; }

        public string PasswordHash { get; set; }

        public bool EmailVerified { get; set; } = true;
        public string? EmailVerificationToken { get; set; }
        public DateTime? EmailVerificationTokenExpiresAt { get; set; }
        public string? PasswordResetToken { get; set; }
        public DateTime? PasswordResetTokenExpiresAt { get; set; }
        public DateTime? TermsAcceptedAt { get; set; }
        public string? AuthProvider { get; set; }

        // Kept for existing role-based authorization.
        public string Role { get; set; } = "Individual";

        // Product-level customer segmentation for plan and dashboard behavior.
        public string CustomerType { get; set; } = "Individual";

        public string? CompanyName { get; set; }

        public string Country { get; set; } = "Unknown";

        public string? PhoneNumber { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        // navigation
        public ICollection<Campaign> Campaigns { get; set; } = new List<Campaign>();
        public Influencer InfluencerProfile { get; set; }
        public ICollection<UserSubscription> Subscriptions { get; set; } = new List<UserSubscription>();
        public ICollection<RefreshToken> RefreshTokens { get; set; } = new List<RefreshToken>();
        public ICollection<IdempotencyRecord> IdempotencyRecords { get; set; } = new List<IdempotencyRecord>();
        public ICollection<Invoice> Invoices { get; set; } = new List<Invoice>();
    }
}