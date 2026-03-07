using System.Threading.Tasks;
using System.Security.Claims;
using InfluencerMatch.Application.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace InfluencerMatch.API.Controllers
{
    [ApiController]
    [Route("api/notifications")]
    [Authorize]
    public class NotificationsController : ControllerBase
    {
        private readonly INotificationService _notifications;

        public NotificationsController(INotificationService notifications)
        {
            _notifications = notifications;
        }

        [HttpGet]
        public async Task<IActionResult> List([FromQuery] bool unreadOnly = false, [FromQuery] int take = 30)
        {
            var userId = GetUserId();
            var items = await _notifications.GetUserNotificationsAsync(userId, unreadOnly, take);
            var unreadCount = await _notifications.GetUnreadCountAsync(userId);
            return Ok(new { items, unreadCount });
        }

        [HttpPost("{id:int}/read")]
        public async Task<IActionResult> MarkRead(int id)
        {
            await _notifications.MarkAsReadAsync(GetUserId(), id);
            return Ok(new { ok = true });
        }

        [HttpPost("read-all")]
        public async Task<IActionResult> ReadAll()
        {
            await _notifications.MarkAllAsReadAsync(GetUserId());
            return Ok(new { ok = true });
        }

        private int GetUserId() => int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
    }
}
