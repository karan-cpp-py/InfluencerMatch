namespace InfluencerMatch.Domain.Entities
{
    public class CollaborationMilestone
    {
        public int CollaborationMilestoneId { get; set; }

        public int RequestId { get; set; }
        public CollaborationRequest Request { get; set; } = null!;

        public string Title { get; set; } = string.Empty;
        public string? Description { get; set; }
        public DateTime? DueDate { get; set; }

        // Pending | InProgress | Submitted | RevisionRequested | Approved | Completed
        public string Status { get; set; } = "Pending";

        public string? DeliverableUrl { get; set; }
        public string? RevisionNotes { get; set; }
        public int RevisionCount { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime? UpdatedAt { get; set; }
    }
}
