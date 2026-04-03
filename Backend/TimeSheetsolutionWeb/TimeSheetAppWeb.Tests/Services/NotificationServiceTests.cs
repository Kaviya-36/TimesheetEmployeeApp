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

    public class NotificationServiceEdgeCaseTests
    {
        private readonly Mock<IHubContext<NotificationHub>> _hubContext  = new();
        private readonly Mock<IHubClients>                  _clients     = new();
        private readonly Mock<IClientProxy>                 _clientProxy = new();

        private NotificationService CreateService()
        {
            _hubContext.Setup(h => h.Clients).Returns(_clients.Object);
            _clients.Setup(c => c.Group(It.IsAny<string>())).Returns(_clientProxy.Object);
            _clientProxy.Setup(p => p.SendCoreAsync(
                It.IsAny<string>(), It.IsAny<object[]>(), It.IsAny<CancellationToken>()))
                .Returns(Task.CompletedTask);
            return new NotificationService(_hubContext.Object);
        }

        // ── SendToUser — user ID zero ─────────────────────────────────────────

        [Fact]
        public async Task SendToUser_ZeroUserId_CallsGroup_user_0()
        {
            var svc = CreateService();

            await svc.SendToUserAsync(0, "Type", "Message");

            _clients.Verify(c => c.Group("user_0"), Times.Once);
        }

        // ── SendToUser — empty message ────────────────────────────────────────

        [Fact]
        public async Task SendToUser_EmptyMessage_StillSends()
        {
            var svc = CreateService();

            var ex = await Record.ExceptionAsync(() =>
                svc.SendToUserAsync(1, "Type", string.Empty));

            Assert.Null(ex);
        }

        // ── SendToRole — empty role ───────────────────────────────────────────

        [Fact]
        public async Task SendToRole_EmptyRole_CallsGroup_role_Empty()
        {
            var svc = CreateService();

            await svc.SendToRoleAsync(string.Empty, "Type", "Message");

            _clients.Verify(c => c.Group("role_"), Times.Once);
        }

        // ── SendToRole — multiple calls same role ─────────────────────────────

        [Fact]
        public async Task SendToRole_CalledMultipleTimes_SendsEachTime()
        {
            var svc = CreateService();

            await svc.SendToRoleAsync("Manager", "T", "M1");
            await svc.SendToRoleAsync("Manager", "T", "M2");

            _clientProxy.Verify(p => p.SendCoreAsync(
                "ReceiveNotification", It.IsAny<object[]>(), It.IsAny<CancellationToken>()),
                Times.Exactly(2));
        }

        // ── SendToUser — payload contains type and message ────────────────────

        [Fact]
        public async Task SendToUser_PayloadContainsTypeAndMessage()
        {
            var svc = CreateService();

            await svc.SendToUserAsync(5, "Payroll", "Salary processed.");

            _clientProxy.Verify(p => p.SendCoreAsync(
                "ReceiveNotification",
                It.Is<object[]>(args =>
                    args[0].ToString() == "Payroll" &&
                    args[1].ToString() == "Salary processed."),
                It.IsAny<CancellationToken>()), Times.Once);
        }

        // ── SendToRole — payload contains type and message ────────────────────

        [Fact]
        public async Task SendToRole_PayloadContainsTypeAndMessage()
        {
            var svc = CreateService();

            await svc.SendToRoleAsync("HR", "Attendance", "Missed checkout detected.");

            _clientProxy.Verify(p => p.SendCoreAsync(
                "ReceiveNotification",
                It.Is<object[]>(args =>
                    args[0].ToString() == "Attendance" &&
                    args[1].ToString() == "Missed checkout detected."),
                It.IsAny<CancellationToken>()), Times.Once);
        }
    }

    public class NotificationServiceAdditionalTests
    {
        private readonly Mock<IHubContext<NotificationHub>> _hubContext  = new();
        private readonly Mock<IHubClients>                  _clients     = new();
        private readonly Mock<IClientProxy>                 _clientProxy = new();

        private NotificationService CreateService()
        {
            _hubContext.Setup(h => h.Clients).Returns(_clients.Object);
            _clients.Setup(c => c.Group(It.IsAny<string>())).Returns(_clientProxy.Object);
            _clientProxy.Setup(p => p.SendCoreAsync(
                It.IsAny<string>(), It.IsAny<object[]>(), It.IsAny<CancellationToken>()))
                .Returns(Task.CompletedTask);
            return new NotificationService(_hubContext.Object);
        }

        // ── SendToUserAsync: sends to correct group format ────────────────────

        [Fact]
        public async Task SendToUser_GroupNameFormat_IsUserUnderscore_Id()
        {
            var svc = CreateService();

            await svc.SendToUserAsync(7, "Type", "Msg");

            _clients.Verify(c => c.Group("user_7"), Times.Once);
        }

        // ── SendToRoleAsync: sends to correct group format ────────────────────

        [Fact]
        public async Task SendToRole_GroupNameFormat_IsRoleUnderscore_RoleName()
        {
            var svc = CreateService();

            await svc.SendToRoleAsync("Admin", "Type", "Msg");

            _clients.Verify(c => c.Group("role_Admin"), Times.Once);
        }

        // ── SendToUserAsync: event name is ReceiveNotification ────────────────

        [Fact]
        public async Task SendToUser_EventName_IsReceiveNotification()
        {
            var svc = CreateService();

            await svc.SendToUserAsync(1, "Leave", "Approved");

            _clientProxy.Verify(p => p.SendCoreAsync(
                "ReceiveNotification", It.IsAny<object[]>(), It.IsAny<CancellationToken>()), Times.Once);
        }

        // ── SendToRoleAsync: event name is ReceiveNotification ────────────────

        [Fact]
        public async Task SendToRole_EventName_IsReceiveNotification()
        {
            var svc = CreateService();

            await svc.SendToRoleAsync("HR", "Payroll", "Generated");

            _clientProxy.Verify(p => p.SendCoreAsync(
                "ReceiveNotification", It.IsAny<object[]>(), It.IsAny<CancellationToken>()), Times.Once);
        }

        // ── SendToUserAsync: large userId works ───────────────────────────────

        [Fact]
        public async Task SendToUser_LargeUserId_CallsCorrectGroup()
        {
            var svc = CreateService();

            await svc.SendToUserAsync(99999, "Type", "Msg");

            _clients.Verify(c => c.Group("user_99999"), Times.Once);
        }

        // ── SendToRoleAsync: all standard roles work ──────────────────────────

        [Theory]
        [InlineData("Employee")]
        [InlineData("Manager")]
        [InlineData("HR")]
        [InlineData("Admin")]
        [InlineData("Mentor")]
        [InlineData("Intern")]
        public async Task SendToRole_AllStandardRoles_CallsCorrectGroup(string role)
        {
            var svc = CreateService();

            await svc.SendToRoleAsync(role, "Type", "Msg");

            _clients.Verify(c => c.Group($"role_{role}"), Times.Once);
        }

        // ── SendToUserAsync: does not throw on null message ───────────────────

        [Fact]
        public async Task SendToUser_NullMessage_DoesNotThrow()
        {
            var svc = CreateService();

            var ex = await Record.ExceptionAsync(() =>
                svc.SendToUserAsync(1, "Type", null!));

            Assert.Null(ex);
        }

        // ── SendToRoleAsync: multiple sequential calls each send ──────────────

        [Fact]
        public async Task SendToRole_ThreeSequentialCalls_SendsThreeTimes()
        {
            var svc = CreateService();

            await svc.SendToRoleAsync("Manager", "T", "M1");
            await svc.SendToRoleAsync("Manager", "T", "M2");
            await svc.SendToRoleAsync("Manager", "T", "M3");

            _clientProxy.Verify(p => p.SendCoreAsync(
                "ReceiveNotification", It.IsAny<object[]>(), It.IsAny<CancellationToken>()),
                Times.Exactly(3));
        }

        // ── SendToUserAsync: payload args length is at least 2 ───────────────

        [Fact]
        public async Task SendToUser_PayloadHasAtLeastTwoArgs()
        {
            var svc = CreateService();

            await svc.SendToUserAsync(1, "Attendance", "Checked in");

            _clientProxy.Verify(p => p.SendCoreAsync(
                "ReceiveNotification",
                It.Is<object[]>(args => args.Length >= 2),
                It.IsAny<CancellationToken>()), Times.Once);
        }
    }
