namespace InfluencerMatch.Domain.Entities
{
    public class FunnelEvent
    {
        public int FunnelEventId { get; set; }
        public int? UserId { get; set; }
        public string? Role { get; set; }
        public string EventName { get; set; } = string.Empty;
        public string? MetadataJson { get; set; }
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    }
}
