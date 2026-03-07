using System.ComponentModel.DataAnnotations;

namespace InfluencerMatch.Domain.Entities
{
    public class WebhookEvent
    {
        [Key]
        public int WebhookEventId { get; set; }

        [Required]
        public string Provider { get; set; } = string.Empty;

        [Required]
        public string EventId { get; set; } = string.Empty;

        [Required]
        public string PayloadHash { get; set; } = string.Empty;

        [Required]
        public string Status { get; set; } = "Processed";

        public string? ErrorMessage { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    }
}
