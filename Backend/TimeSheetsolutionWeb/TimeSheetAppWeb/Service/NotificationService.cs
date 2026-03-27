using Microsoft.AspNetCore.SignalR;
using TimeSheetAppWeb.Interface;

namespace TimeSheetAppWeb.Services
{
    public class NotificationService : INotificationService
    {
        private readonly IHubContext<NotificationHub> _hub;

        public NotificationService(IHubContext<NotificationHub> hub)
        {
            _hub = hub;
        }

        // Push to a specific user by their userId group
        public async Task SendToUserAsync(int userId, string type, string message)
        {
            await _hub.Clients.Group($"user_{userId}")
                .SendAsync("ReceiveNotification", type, message, DateTime.UtcNow.ToString("o"));
        }

        // Push to all connected users with a given role
        public async Task SendToRoleAsync(string role, string type, string message)
        {
            await _hub.Clients.Group($"role_{role}")
                .SendAsync("ReceiveNotification", type, message, DateTime.UtcNow.ToString("o"));
        }
    }
}
