using System.ComponentModel.DataAnnotations;

namespace InfluencerMatch.Domain.Entities
{
    public class RefreshToken
    {
        [Key]
        public int RefreshTokenId { get; set; }

        public int UserId { get; set; }

        [Required]
        public string TokenHash { get; set; } = string.Empty;

        [Required]
        public string TokenFamily { get; set; } = Guid.NewGuid().ToString("N");

        [Required]
        public DateTime ExpiresAt { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        public DateTime? RevokedAt { get; set; }

        public string? ReplacedByTokenHash { get; set; }

        public string? CreatedByIp { get; set; }

        public string? RevokedByIp { get; set; }

        public User? User { get; set; }
    }
}
