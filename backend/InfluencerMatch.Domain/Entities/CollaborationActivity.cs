namespace InfluencerMatch.Domain.Entities
{
    public class CollaborationActivity
    {
        public int CollaborationActivityId { get; set; }

        public int RequestId { get; set; }
        public CollaborationRequest Request { get; set; } = null!;

        public int? ActorUserId { get; set; }
        public string ActorRole { get; set; } = string.Empty;
        public string ActionType { get; set; } = string.Empty;
        public string Message { get; set; } = string.Empty;

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    }
}
