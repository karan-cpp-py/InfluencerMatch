using System.Net;
using System.Net.Mail;
using InfluencerMatch.Application.DTOs;
using InfluencerMatch.Application.Interfaces;
using InfluencerMatch.Domain.Entities;
using InfluencerMatch.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace InfluencerMatch.Infrastructure.Services
{
    public class NotificationService : INotificationService
    {
        private readonly ApplicationDbContext _db;
        private readonly ILogger<NotificationService> _logger;
        private readonly EmailNotificationOptions _emailOptions;

        public NotificationService(
            ApplicationDbContext db,
            ILogger<NotificationService> logger,
            IOptions<EmailNotificationOptions> emailOptions)
        {
            _db = db;
            _logger = logger;
            _emailOptions = emailOptions.Value;
        }

        public async Task NotifyAsync(NotificationCreateRequestDto request)
        {
            if (request.UserId <= 0 || string.IsNullOrWhiteSpace(request.Title))
            {
                return;
            }

            var item = new UserNotification
            {
                UserId = request.UserId,
                Type = request.Type,
                Title = request.Title,
                Message = request.Message,
                Channel = "InApp",
                MetadataJson = request.MetadataJson,
                IsRead = false,
                CreatedAt = DateTime.UtcNow
            };

            _db.UserNotifications.Add(item);

            if (request.SendEmail)
            {
                var user = await _db.Users.AsNoTracking().FirstOrDefaultAsync(x => x.UserId == request.UserId);
                if (!string.IsNullOrWhiteSpace(user?.Email))
                {
                    await TrySendEmailAsync(user.Email, request.Title, request.Message, request.UserId, request.Type);
                }
            }

            await _db.SaveChangesAsync();
        }

        public async Task SendEmailAsync(string toEmail, string subject, string body, string type = "system.email")
        {
            if (string.IsNullOrWhiteSpace(toEmail) || string.IsNullOrWhiteSpace(subject))
            {
                return;
            }

            await TrySendEmailAsync(toEmail.Trim(), subject.Trim(), body ?? string.Empty, 0, type);
        }

        private async Task TrySendEmailAsync(string toEmail, string subject, string body, int userId, string type)
        {
            if (!_emailOptions.Enabled)
            {
                _logger.LogInformation(
                    "Email disabled; notification recorded only. UserId={UserId}, Email={Email}, Type={Type}, Title={Title}",
                    userId,
                    toEmail,
                    type,
                    subject);
                return;
            }

            if (string.IsNullOrWhiteSpace(_emailOptions.SmtpHost)
                || string.IsNullOrWhiteSpace(_emailOptions.FromEmail))
            {
                _logger.LogWarning(
                    "Email enabled but SMTP configuration is incomplete. UserId={UserId}, Email={Email}, Type={Type}",
                    userId,
                    toEmail,
                    type);
                return;
            }

            try
            {
                using var message = new MailMessage
                {
                    From = new MailAddress(_emailOptions.FromEmail, _emailOptions.FromName),
                    Subject = subject,
                    Body = body,
                    IsBodyHtml = false,
                };
                message.To.Add(toEmail);

                using var client = new SmtpClient(_emailOptions.SmtpHost, _emailOptions.SmtpPort)
                {
                    EnableSsl = _emailOptions.UseSsl,
                    DeliveryMethod = SmtpDeliveryMethod.Network,
                    Credentials = string.IsNullOrWhiteSpace(_emailOptions.Username)
                        ? CredentialCache.DefaultNetworkCredentials
                        : new NetworkCredential(_emailOptions.Username, _emailOptions.Password),
                };

                await client.SendMailAsync(message);
                _logger.LogInformation(
                    "Email notification sent. UserId={UserId}, Email={Email}, Type={Type}, Title={Title}",
                    userId,
                    toEmail,
                    type,
                    subject);
            }
            catch (Exception ex)
            {
                _logger.LogError(
                    ex,
                    "Email notification failed. UserId={UserId}, Email={Email}, Type={Type}, Title={Title}",
                    userId,
                    toEmail,
                    type,
                    subject);
            }
        }

        public async Task<List<UserNotificationDto>> GetUserNotificationsAsync(int userId, bool unreadOnly, int take = 40)
        {
            take = Math.Clamp(take, 1, 100);

            var query = _db.UserNotifications
                .AsNoTracking()
                .Where(x => x.UserId == userId);

            if (unreadOnly)
            {
                query = query.Where(x => !x.IsRead);
            }

            return await query
                .OrderByDescending(x => x.CreatedAt)
                .Take(take)
                .Select(x => new UserNotificationDto
                {
                    UserNotificationId = x.UserNotificationId,
                    Type = x.Type,
                    Title = x.Title,
                    Message = x.Message,
                    Channel = x.Channel,
                    IsRead = x.IsRead,
                    CreatedAt = x.CreatedAt
                })
                .ToListAsync();
        }

        public Task<int> GetUnreadCountAsync(int userId)
            => _db.UserNotifications.CountAsync(x => x.UserId == userId && !x.IsRead);

        public async Task MarkAsReadAsync(int userId, int notificationId)
        {
            var item = await _db.UserNotifications.FirstOrDefaultAsync(x => x.UserNotificationId == notificationId && x.UserId == userId);
            if (item == null || item.IsRead)
            {
                return;
            }

            item.IsRead = true;
            item.ReadAt = DateTime.UtcNow;
            await _db.SaveChangesAsync();
        }

        public async Task MarkAllAsReadAsync(int userId)
        {
            var items = await _db.UserNotifications
                .Where(x => x.UserId == userId && !x.IsRead)
                .ToListAsync();

            if (items.Count == 0)
            {
                return;
            }

            var now = DateTime.UtcNow;
            foreach (var item in items)
            {
                item.IsRead = true;
                item.ReadAt = now;
            }

            await _db.SaveChangesAsync();
        }
    }
}
