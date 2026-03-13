namespace InfluencerMatch.Application.DTOs
{
    public class CollaborationMilestoneDto
    {
        public int CollaborationMilestoneId { get; set; }
        public int RequestId { get; set; }
        public string Title { get; set; } = string.Empty;
        public string? Description { get; set; }
        public DateTime? DueDate { get; set; }
        public string Status { get; set; } = string.Empty;
        public string? DeliverableUrl { get; set; }
        public string? RevisionNotes { get; set; }
        public int RevisionCount { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime? UpdatedAt { get; set; }
    }

    public class CollaborationActivityDto
    {
        public int CollaborationActivityId { get; set; }
        public int RequestId { get; set; }
        public string ActorRole { get; set; } = string.Empty;
        public string ActionType { get; set; } = string.Empty;
        public string Message { get; set; } = string.Empty;
        public DateTime CreatedAt { get; set; }
    }

    public class AddMilestoneDto
    {
        public string Title { get; set; } = string.Empty;
        public string? Description { get; set; }
        public DateTime? DueDate { get; set; }
    }

    public class UpdateMilestoneStatusDto
    {
        public string Status { get; set; } = string.Empty;
        public string? DeliverableUrl { get; set; }
    }

    public class MilestoneRevisionDto
    {
        public string RevisionNotes { get; set; } = string.Empty;
    }

    public class CollaborationStageUpdateDto
    {
        public string? Notes { get; set; }
    }

    public class CollaborationWorkflowDto
    {
        public CollaborationRequestDto Request { get; set; } = new();
        public List<CollaborationMilestoneDto> Milestones { get; set; } = new();
        public List<CollaborationActivityDto> ActivityFeed { get; set; } = new();
        public int CompletionPercent { get; set; }
        public bool IsCompleted { get; set; }
    }
}
