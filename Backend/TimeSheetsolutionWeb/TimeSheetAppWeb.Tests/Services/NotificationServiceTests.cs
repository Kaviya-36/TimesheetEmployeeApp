using Microsoft.AspNetCore.SignalR;
using Moq;
using TimeSheetAppWeb.Services;
using Xunit;

namespace TimeSheetAppWeb.Tests.Services
{
    public class NotificationServiceTests
    {
        private readonly Mock<IHubContext<NotificationHub>> _hubContext = new();
        private readonly Mock<IHubClients>                  _clients    = new();
        private readonly Mock<IClientProxy>                 _clientProxy = new();

        private NotificationService CreateService()
        {
            _hubContext.Setup(h => h.Clients).Returns(_clients.Object);
            _clients.Setup(c => c.Group(It.IsAny<string>())).Returns(_clientProxy.Object);
            _clientProxy.Setup(p => p.SendCoreAsync(
                It.IsAny<string>(),
                It.IsAny<object[]>(),
                It.IsAny<CancellationToken>()))
                .Returns(Task.CompletedTask);
            return new NotificationService(_hubContext.Object);
        }

        // ── SendToUserAsync ────────────────────────────────────────────────────

        [Fact]
        public async Task SendToUser_CallsCorrectGroup()
        {
            var svc = CreateService();

            await svc.SendToUserAsync(42, "Timesheet", "Your timesheet was approved.");

            _clients.Verify(c => c.Group("user_42"), Times.Once);
        }

        [Fact]
        public async Task SendToUser_SendsReceiveNotificationEvent()
        {
            var svc = CreateService();

            await svc.SendToUserAsync(1, "Leave", "Leave approved.");

            _clientProxy.Verify(p => p.SendCoreAsync(
                "ReceiveNotification",
                It.Is<object[]>(args =>
                    args.Length >= 2 &&
                    args[0].ToString() == "Leave" &&
                    args[1].ToString() == "Leave approved."),
                It.IsAny<CancellationToken>()), Times.Once);
        }

        [Fact]
        public async Task SendToUser_DifferentUsers_CallsDifferentGroups()
        {
            var svc = CreateService();

            await svc.SendToUserAsync(1, "Type", "Msg");
            await svc.SendToUserAsync(2, "Type", "Msg");

            _clients.Verify(c => c.Group("user_1"), Times.Once);
            _clients.Verify(c => c.Group("user_2"), Times.Once);
        }

        // ── SendToRoleAsync ────────────────────────────────────────────────────

        [Fact]
        public async Task SendToRole_CallsCorrectGroup()
        {
            var svc = CreateService();

            await svc.SendToRoleAsync("Manager", "Timesheet", "New timesheet submitted.");

            _clients.Verify(c => c.Group("role_Manager"), Times.Once);
        }

        [Fact]
        public async Task SendToRole_SendsReceiveNotificationEvent()
        {
            var svc = CreateService();

            await svc.SendToRoleAsync("HR", "Leave", "Leave request pending.");

            _clientProxy.Verify(p => p.SendCoreAsync(
                "ReceiveNotification",
                It.Is<object[]>(args =>
                    args.Length >= 2 &&
                    args[0].ToString() == "Leave" &&
                    args[1].ToString() == "Leave request pending."),
                It.IsAny<CancellationToken>()), Times.Once);
        }

        [Fact]
        public async Task SendToRole_DifferentRoles_CallsDifferentGroups()
        {
            var svc = CreateService();

            await svc.SendToRoleAsync("Manager", "T", "M");
            await svc.SendToRoleAsync("HR", "T", "M");

            _clients.Verify(c => c.Group("role_Manager"), Times.Once);
            _clients.Verify(c => c.Group("role_HR"), Times.Once);
        }

        [Fact]
        public async Task SendToRole_CompletesWithoutException()
        {
            var svc = CreateService();

            var ex = await Record.ExceptionAsync(() =>
                svc.SendToRoleAsync("Admin", "System", "Maintenance scheduled."));

            Assert.Null(ex);
        }
    }
}
