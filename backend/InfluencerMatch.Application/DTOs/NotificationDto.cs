namespace InfluencerMatch.Application.DTOs
{
    public class UserNotificationDto
    {
        public int UserNotificationId { get; set; }
        public string Type { get; set; } = string.Empty;
        public string Title { get; set; } = string.Empty;
        public string Message { get; set; } = string.Empty;
        public string Channel { get; set; } = "InApp";
        public bool IsRead { get; set; }
        public DateTime CreatedAt { get; set; }
    }

    public class NotificationCreateRequestDto
    {
        public int UserId { get; set; }
        public string Type { get; set; } = string.Empty;
        public string Title { get; set; } = string.Empty;
        public string Message { get; set; } = string.Empty;
        public bool SendEmail { get; set; }
        public string? MetadataJson { get; set; }
    }
}
