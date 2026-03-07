using System.ComponentModel.DataAnnotations;

namespace InfluencerMatch.Domain.Entities
{
    public class IdempotencyRecord
    {
        [Key]
        public int IdempotencyRecordId { get; set; }

        public int UserId { get; set; }

        [Required]
        public string Scope { get; set; } = string.Empty;

        [Required]
        public string IdempotencyKey { get; set; } = string.Empty;

        [Required]
        public string RequestHash { get; set; } = string.Empty;

        [Required]
        public string ResponseJson { get; set; } = string.Empty;

        public int StatusCode { get; set; } = 200;

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        public DateTime ExpiresAt { get; set; } = DateTime.UtcNow.AddDays(2);

        public User? User { get; set; }
    }
}
