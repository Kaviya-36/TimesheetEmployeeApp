using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using TimeSheetAppWeb.Interface;

namespace TimeSheetAppWeb.Controllers
{
    [ApiController]
    [Route("api/notif-test")]
    [Authorize]
    public class NotificationTestController : ControllerBase
    {
        private readonly INotificationService _notif;
        private readonly IHubContext<NotificationHub> _hub;

        public NotificationTestController(INotificationService notif, IHubContext<NotificationHub> hub)
        {
            _notif = notif;
            _hub   = hub;
        }

        // Broadcast to ALL connected clients (no groups)
        [HttpGet("all")]
        public async Task<IActionResult> TestAll()
        {
            await _hub.Clients.All.SendAsync("ReceiveNotification", "System", $"Broadcast to ALL at {DateTime.Now:HH:mm:ss}", DateTime.UtcNow.ToString("o"));
            return Ok("Broadcast sent to all clients");
        }

        // Send to specific user group
        [HttpGet("user/{userId}")]
        public async Task<IActionResult> TestUser(int userId)
        {
            await _notif.SendToUserAsync(userId, "System", $"Test to user {userId} at {DateTime.Now:HH:mm:ss}");
            return Ok($"Sent to user_{userId}");
        }

        // Send to role group
        [HttpGet("role/{role}")]
        public async Task<IActionResult> TestRole(string role)
        {
            await _notif.SendToRoleAsync(role, "System", $"Test to role {role} at {DateTime.Now:HH:mm:ss}");
            return Ok($"Sent to role_{role}");
        }
    }
}
