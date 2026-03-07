using System.ComponentModel.DataAnnotations;

namespace InfluencerMatch.Domain.Entities
{
    public class BrandWaitlistEntry
    {
        [Key]
        public int BrandWaitlistEntryId { get; set; }

        [Required]
        [EmailAddress]
        public string Email { get; set; } = string.Empty;

        [Required]
        [MaxLength(100)]
        public string CompanyName { get; set; } = string.Empty;

        [Required]
        [MaxLength(30)]
        public string CustomerType { get; set; } = "Brand";

        [MaxLength(30)]
        public string Role { get; set; } = "Brand";

        public int? UserId { get; set; }

        [MaxLength(500)]
        public string? Notes { get; set; }

        [Required]
        [MaxLength(30)]
        public string Status { get; set; } = "Pending";

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        public User? User { get; set; }
    }
}
