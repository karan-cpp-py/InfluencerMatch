using InfluencerMatch.Application.DTOs;

namespace InfluencerMatch.Application.Interfaces
{
    public interface INotificationService
    {
        Task NotifyAsync(NotificationCreateRequestDto request);
        Task SendEmailAsync(string toEmail, string subject, string body, string type = "system.email");
        Task<List<UserNotificationDto>> GetUserNotificationsAsync(int userId, bool unreadOnly, int take = 40);
        Task<int> GetUnreadCountAsync(int userId);
        Task MarkAsReadAsync(int userId, int notificationId);
        Task MarkAllAsReadAsync(int userId);
    }
}
