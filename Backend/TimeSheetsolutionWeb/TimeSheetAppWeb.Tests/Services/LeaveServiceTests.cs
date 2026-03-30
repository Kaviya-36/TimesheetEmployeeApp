using Microsoft.Extensions.Logging;
using Moq;
using TimeSheetAppWeb.Interface;
using TimeSheetAppWeb.Model;
using TimeSheetAppWeb.Model.DTOs;
using TimeSheetAppWeb.Services;
using Xunit;

namespace TimeSheetAppWeb.Tests.Services
{
    public class LeaveServiceTests
    {
        private readonly Mock<IRepository<int, LeaveRequest>> _leaveRepo = new();
        private readonly Mock<IRepository<int, User>>         _userRepo  = new();
        private readonly Mock<IRepository<int, LeaveType>>    _typeRepo  = new();
        private readonly Mock<INotificationService>           _notif     = new();
        private readonly Mock<ILogger<LeaveService>>          _logger    = new();

        // Constructor: leaveRepo, userRepo, leaveTypeRepo, notif, logger
        private LeaveService CreateService() =>
            new(_leaveRepo.Object, _userRepo.Object, _typeRepo.Object, _notif.Object, _logger.Object);

        private User MakeUser(int id = 1) => new User
        {
            Id = id, Name = "Test User", Email = "test@test.com",
            EmployeeId = "E001", Role = UserRole.Employee,
            PasswordHash = "hash", IsActive = true
        };

        private LeaveType MakeType(int id = 1) => new LeaveType
        {
            Id = id, Name = "Annual", MaxDaysPerYear = 20, IsActive = true
        };

        // ── ApplyLeaveAsync ────────────────────────────────────────────────────

        [Fact]
        public async Task ApplyLeave_PastDate_ReturnsFail()
        {
            var svc = CreateService();
            var req = new LeaveCreateRequest
            {
                LeaveTypeId = 1,
                FromDate = DateTime.Today.AddDays(-1),
                ToDate   = DateTime.Today
            };

            var result = await svc.ApplyLeaveAsync(1, req);

            Assert.False(result.Success);
            Assert.Contains("past", result.Message, StringComparison.OrdinalIgnoreCase);
        }

        [Fact]
        public async Task ApplyLeave_ToBeforeFrom_ReturnsFail()
        {
            var svc = CreateService();
            var req = new LeaveCreateRequest
            {
                LeaveTypeId = 1,
                FromDate = DateTime.Today.AddDays(5),
                ToDate   = DateTime.Today.AddDays(2)
            };

            var result = await svc.ApplyLeaveAsync(1, req);

            Assert.False(result.Success);
        }

        [Fact]
        public async Task ApplyLeave_UserNotFound_ReturnsFail()
        {
            _userRepo.Setup(r => r.GetByIdAsync(1)).ReturnsAsync((User?)null);
            var svc = CreateService();
            var req = new LeaveCreateRequest
            {
                LeaveTypeId = 1,
                FromDate = DateTime.Today.AddDays(1),
                ToDate   = DateTime.Today.AddDays(3)
            };

            var result = await svc.ApplyLeaveAsync(1, req);

            Assert.False(result.Success);
        }

        [Fact]
        public async Task ApplyLeave_ValidRequest_ReturnsSuccess()
        {
            var user = MakeUser();
            var type = MakeType();
            _userRepo.Setup(r => r.GetByIdAsync(1)).ReturnsAsync(user);
            _typeRepo.Setup(r => r.GetByIdAsync(1)).ReturnsAsync(type);
            _typeRepo.Setup(r => r.GetAllAsync()).ReturnsAsync(new List<LeaveType> { type });
            _leaveRepo.Setup(r => r.GetAllAsync()).ReturnsAsync(new List<LeaveRequest>());
            _leaveRepo.Setup(r => r.AddAsync(It.IsAny<LeaveRequest>())).ReturnsAsync(new LeaveRequest
            {
                Id = 1, UserId = 1, LeaveTypeId = 1,
                FromDate = DateTime.Today.AddDays(1),
                ToDate   = DateTime.Today.AddDays(3),
                Status   = LeaveStatus.Pending
            });
            _notif.Setup(n => n.SendToRoleAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()))
                  .Returns(Task.CompletedTask);

            var svc = CreateService();
            var req = new LeaveCreateRequest
            {
                LeaveTypeId = 1,
                FromDate = DateTime.Today.AddDays(1),
                ToDate   = DateTime.Today.AddDays(3)
            };

            var result = await svc.ApplyLeaveAsync(1, req);

            Assert.True(result.Success);
        }

        // ── ApproveOrRejectLeaveAsync ──────────────────────────────────────────

        [Fact]
        public async Task ApproveLeave_SelfApproval_ReturnsFail()
        {
            var leave = new LeaveRequest { Id = 1, UserId = 5, Status = LeaveStatus.Pending, LeaveTypeId = 1, FromDate = DateTime.Today, ToDate = DateTime.Today };
            _leaveRepo.Setup(r => r.GetByIdAsync(1)).ReturnsAsync(leave);

            var svc = CreateService();
            var req = new LeaveApprovalRequest { LeaveId = 1, ApprovedById = 5, IsApproved = true };

            var result = await svc.ApproveOrRejectLeaveAsync(req);

            Assert.False(result.Success);
            Assert.Contains("own", result.Message, StringComparison.OrdinalIgnoreCase);
        }

        [Fact]
        public async Task ApproveLeave_AlreadyProcessed_ReturnsFail()
        {
            var leave = new LeaveRequest { Id = 1, UserId = 5, Status = LeaveStatus.Approved, LeaveTypeId = 1, FromDate = DateTime.Today, ToDate = DateTime.Today };
            _leaveRepo.Setup(r => r.GetByIdAsync(1)).ReturnsAsync(leave);

            var svc = CreateService();
            var req = new LeaveApprovalRequest { LeaveId = 1, ApprovedById = 10, IsApproved = true };

            var result = await svc.ApproveOrRejectLeaveAsync(req);

            Assert.False(result.Success);
        }

        [Fact]
        public async Task ApproveLeave_ValidRequest_ReturnsSuccess()
        {
            var leave = new LeaveRequest { Id = 1, UserId = 5, Status = LeaveStatus.Pending, LeaveTypeId = 1, FromDate = DateTime.Today, ToDate = DateTime.Today };
            _leaveRepo.Setup(r => r.GetByIdAsync(1)).ReturnsAsync(leave);
            _leaveRepo.Setup(r => r.UpdateAsync(1, It.IsAny<LeaveRequest>())).ReturnsAsync(leave);
            _notif.Setup(n => n.SendToUserAsync(It.IsAny<int>(), It.IsAny<string>(), It.IsAny<string>()))
                  .Returns(Task.CompletedTask);

            var svc = CreateService();
            var req = new LeaveApprovalRequest { LeaveId = 1, ApprovedById = 10, IsApproved = true, ManagerComment = "Approved" };

            var result = await svc.ApproveOrRejectLeaveAsync(req);

            Assert.True(result.Success);
        }
    }
}
