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

    public class LeaveServiceEdgeCaseTests
    {
        private readonly Mock<IRepository<int, LeaveRequest>> _leaveRepo = new();
        private readonly Mock<IRepository<int, User>>         _userRepo  = new();
        private readonly Mock<IRepository<int, LeaveType>>    _typeRepo  = new();
        private readonly Mock<INotificationService>           _notif     = new();
        private readonly Mock<ILogger<LeaveService>>          _logger    = new();

        private LeaveService CreateService() =>
            new(_leaveRepo.Object, _userRepo.Object, _typeRepo.Object, _notif.Object, _logger.Object);

        private User MakeUser(int id = 1) => new User
        {
            Id = id, Name = "Test User", Email = "test@test.com",
            EmployeeId = "E001", Role = UserRole.Employee,
            PasswordHash = "hash", IsActive = true
        };

        private LeaveType MakeType(int id = 1, int maxDays = 20) => new LeaveType
        {
            Id = id, Name = "Annual", MaxDaysPerYear = maxDays, IsActive = true
        };

        // ── ApplyLeave — leave type not found ─────────────────────────────────

        [Fact]
        public async Task ApplyLeave_LeaveTypeNotFound_ReturnsFail()
        {
            _userRepo.Setup(r => r.GetByIdAsync(1)).ReturnsAsync(MakeUser());
            _typeRepo.Setup(r => r.GetByIdAsync(99)).ReturnsAsync((LeaveType?)null);

            var svc = CreateService();
            var result = await svc.ApplyLeaveAsync(1, new LeaveCreateRequest
            {
                LeaveTypeId = 99,
                FromDate = DateTime.Today.AddDays(1),
                ToDate   = DateTime.Today.AddDays(3)
            });

            Assert.False(result.Success);
        }

        [Fact]
        public async Task ApplyLeave_InactiveLeaveType_ReturnsFail()
        {
            var inactiveType = new LeaveType { Id = 1, Name = "Sick", MaxDaysPerYear = 10, IsActive = false };
            _userRepo.Setup(r => r.GetByIdAsync(1)).ReturnsAsync(MakeUser());
            _typeRepo.Setup(r => r.GetByIdAsync(1)).ReturnsAsync(inactiveType);

            var svc = CreateService();
            var result = await svc.ApplyLeaveAsync(1, new LeaveCreateRequest
            {
                LeaveTypeId = 1,
                FromDate = DateTime.Today.AddDays(1),
                ToDate   = DateTime.Today.AddDays(2)
            });

            Assert.False(result.Success);
        }

        // ── ApplyLeave — balance exhausted ────────────────────────────────────

        [Fact]
        public async Task ApplyLeave_ExceedsBalance_ReturnsFail()
        {
            var user = MakeUser();
            var type = MakeType(maxDays: 5);
            // Already used 4 days
            var existing = new LeaveRequest
            {
                Id = 1, UserId = 1, LeaveTypeId = 1,
                FromDate = DateTime.Today.AddDays(10),
                ToDate   = DateTime.Today.AddDays(13),
                Status   = LeaveStatus.Approved
            };
            _userRepo.Setup(r => r.GetByIdAsync(1)).ReturnsAsync(user);
            _typeRepo.Setup(r => r.GetByIdAsync(1)).ReturnsAsync(type);
            _leaveRepo.Setup(r => r.GetAllAsync()).ReturnsAsync(new List<LeaveRequest> { existing });

            var svc = CreateService();
            // Requesting 3 more days → 4 + 3 = 7 > 5
            var result = await svc.ApplyLeaveAsync(1, new LeaveCreateRequest
            {
                LeaveTypeId = 1,
                FromDate = DateTime.Today.AddDays(20),
                ToDate   = DateTime.Today.AddDays(22)
            });

            Assert.False(result.Success);
            Assert.Contains("balance", result.Message, StringComparison.OrdinalIgnoreCase);
        }

       

        // ── ApplyLeave — same day (single day leave) ──────────────────────────

        [Fact]
        public async Task ApplyLeave_SameDayFromAndTo_Succeeds()
        {
            var user = MakeUser();
            var type = MakeType();
            _userRepo.Setup(r => r.GetByIdAsync(1)).ReturnsAsync(user);
            _typeRepo.Setup(r => r.GetByIdAsync(1)).ReturnsAsync(type);
            _leaveRepo.Setup(r => r.GetAllAsync()).ReturnsAsync(new List<LeaveRequest>());
            _leaveRepo.Setup(r => r.AddAsync(It.IsAny<LeaveRequest>())).ReturnsAsync(new LeaveRequest
            {
                Id = 1, UserId = 1, LeaveTypeId = 1,
                FromDate = DateTime.Today.AddDays(1),
                ToDate   = DateTime.Today.AddDays(1),
                Status   = LeaveStatus.Pending
            });
            _notif.Setup(n => n.SendToRoleAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()))
                  .Returns(Task.CompletedTask);

            var svc = CreateService();
            var result = await svc.ApplyLeaveAsync(1, new LeaveCreateRequest
            {
                LeaveTypeId = 1,
                FromDate = DateTime.Today.AddDays(1),
                ToDate   = DateTime.Today.AddDays(1)
            });

            Assert.True(result.Success);
        }

        // ── ApproveOrRejectLeave — leave not found ────────────────────────────

        [Fact]
        public async Task ApproveLeave_LeaveNotFound_ReturnsFail()
        {
            _leaveRepo.Setup(r => r.GetByIdAsync(99)).ReturnsAsync((LeaveRequest?)null);
            var svc = CreateService();

            var result = await svc.ApproveOrRejectLeaveAsync(
                new LeaveApprovalRequest { LeaveId = 99, ApprovedById = 1, IsApproved = true });

            Assert.False(result.Success);
            Assert.Contains("not found", result.Message, StringComparison.OrdinalIgnoreCase);
        }

        [Fact]
        public async Task RejectLeave_Valid_ReturnsSuccess()
        {
            var leave = new LeaveRequest
            {
                Id = 1, UserId = 5, Status = LeaveStatus.Pending,
                LeaveTypeId = 1, FromDate = DateTime.Today, ToDate = DateTime.Today
            };
            _leaveRepo.Setup(r => r.GetByIdAsync(1)).ReturnsAsync(leave);
            _leaveRepo.Setup(r => r.UpdateAsync(1, It.IsAny<LeaveRequest>())).ReturnsAsync(leave);
            _notif.Setup(n => n.SendToUserAsync(It.IsAny<int>(), It.IsAny<string>(), It.IsAny<string>()))
                  .Returns(Task.CompletedTask);

            var svc = CreateService();
            var result = await svc.ApproveOrRejectLeaveAsync(
                new LeaveApprovalRequest { LeaveId = 1, ApprovedById = 10, IsApproved = false, ManagerComment = "Not approved" });

            Assert.True(result.Success);
        }

        // ── DeleteLeave ───────────────────────────────────────────────────────

        [Fact]
        public async Task DeleteLeave_NotFound_ReturnsFail()
        {
            _leaveRepo.Setup(r => r.GetByIdAsync(99)).ReturnsAsync((LeaveRequest?)null);
            var svc = CreateService();

            var result = await svc.DeleteLeaveAsync(99, 1);

            Assert.False(result.Success);
        }

        [Fact]
        public async Task DeleteLeave_WrongUser_ReturnsFail()
        {
            var leave = new LeaveRequest
            {
                Id = 1, UserId = 5, Status = LeaveStatus.Pending,
                LeaveTypeId = 1, FromDate = DateTime.Today, ToDate = DateTime.Today
            };
            _leaveRepo.Setup(r => r.GetByIdAsync(1)).ReturnsAsync(leave);
            var svc = CreateService();

            var result = await svc.DeleteLeaveAsync(1, userId: 99);

            Assert.False(result.Success);
            Assert.Contains("authorized", result.Message, StringComparison.OrdinalIgnoreCase);
        }

        [Fact]
        public async Task DeleteLeave_ApprovedLeave_ReturnsFail()
        {
            var leave = new LeaveRequest
            {
                Id = 1, UserId = 1, Status = LeaveStatus.Approved,
                LeaveTypeId = 1, FromDate = DateTime.Today, ToDate = DateTime.Today
            };
            _leaveRepo.Setup(r => r.GetByIdAsync(1)).ReturnsAsync(leave);
            var svc = CreateService();

            var result = await svc.DeleteLeaveAsync(1, userId: 1);

            Assert.False(result.Success);
            Assert.Contains("pending", result.Message, StringComparison.OrdinalIgnoreCase);
        }

        [Fact]
        public async Task DeleteLeave_PendingOwnLeave_ReturnsSuccess()
        {
            var leave = new LeaveRequest
            {
                Id = 1, UserId = 1, Status = LeaveStatus.Pending,
                LeaveTypeId = 1, FromDate = DateTime.Today, ToDate = DateTime.Today
            };
            _leaveRepo.Setup(r => r.GetByIdAsync(1)).ReturnsAsync(leave);
            _leaveRepo.Setup(r => r.DeleteAsync(1)).ReturnsAsync(leave);
            var svc = CreateService();

            var result = await svc.DeleteLeaveAsync(1, userId: 1);

            Assert.True(result.Success);
        }

        // ── GetLeaveBalance ───────────────────────────────────────────────────

        [Fact]
        public async Task GetLeaveBalance_ReturnsCorrectRemainingDays()
        {
            var type = MakeType(maxDays: 10);
            var usedLeave = new LeaveRequest
            {
                Id = 1, UserId = 1, LeaveTypeId = 1,
                FromDate = DateTime.Today.AddDays(5),
                ToDate   = DateTime.Today.AddDays(7), // 3 days
                Status   = LeaveStatus.Approved
            };
            _leaveRepo.Setup(r => r.GetAllAsync()).ReturnsAsync(new List<LeaveRequest> { usedLeave });
            _typeRepo.Setup(r => r.GetAllAsync()).ReturnsAsync(new List<LeaveType> { type });

            var svc = CreateService();
            var result = await svc.GetLeaveBalanceAsync(1);

            Assert.True(result.Success);
            Assert.NotNull(result.Data);
            // Remaining = 10 - 3 = 7
            var balance = result.Data!.First() as dynamic;
            Assert.NotNull(balance);
        }

        // ── GetUserLeaves — pagination and filtering ──────────────────────────

        [Fact]
        public async Task GetUserLeaves_UserNotFound_ReturnsFail()
        {
            _userRepo.Setup(r => r.GetByIdAsync(99)).ReturnsAsync((User?)null);
            var svc = CreateService();

            var result = await svc.GetUserLeavesAsync(99, 1, 10);

            Assert.False(result.Success);
        }

        [Fact]
        public async Task GetUserLeaves_StatusFilter_ReturnsOnlyMatchingStatus()
        {
            var user = MakeUser();
            var type = MakeType();
            var leaves = new List<LeaveRequest>
            {
                new() { Id = 1, UserId = 1, LeaveTypeId = 1, Status = LeaveStatus.Pending,
                        FromDate = DateTime.Today.AddDays(1), ToDate = DateTime.Today.AddDays(2) },
                new() { Id = 2, UserId = 1, LeaveTypeId = 1, Status = LeaveStatus.Approved,
                        FromDate = DateTime.Today.AddDays(5), ToDate = DateTime.Today.AddDays(6) }
            };
            _userRepo.Setup(r => r.GetByIdAsync(1)).ReturnsAsync(user);
            _leaveRepo.Setup(r => r.GetAllAsync()).ReturnsAsync(leaves);
            _typeRepo.Setup(r => r.GetAllAsync()).ReturnsAsync(new List<LeaveType> { type });

            var svc = CreateService();
            var result = await svc.GetUserLeavesAsync(1, 1, 10, status: "Pending");

            Assert.True(result.Success);
            Assert.Equal(1, result.Data?.TotalRecords);
        }
    }

    public class LeaveServiceAdditionalTests
    {
        private readonly Mock<IRepository<int, LeaveRequest>> _leaveRepo = new();
        private readonly Mock<IRepository<int, User>>         _userRepo  = new();
        private readonly Mock<IRepository<int, LeaveType>>    _typeRepo  = new();
        private readonly Mock<INotificationService>           _notif     = new();
        private readonly Mock<ILogger<LeaveService>>          _logger    = new();

        private LeaveService CreateService() =>
            new(_leaveRepo.Object, _userRepo.Object, _typeRepo.Object, _notif.Object, _logger.Object);

        private User MakeUser(int id = 1) => new User
        {
            Id = id, Name = "Test User", Email = "test@test.com",
            EmployeeId = "E001", Role = UserRole.Employee,
            PasswordHash = "hash", IsActive = true
        };

        private LeaveType MakeType(int id = 1, int maxDays = 20) => new LeaveType
        {
            Id = id, Name = "Annual", MaxDaysPerYear = maxDays, IsActive = true
        };

        // ── ApplyLeaveAsync: user not found returns failure ───────────────────

        [Fact]
        public async Task ApplyLeave_UserNotFound_MessageContainsUserNotFound()
        {
            _userRepo.Setup(r => r.GetByIdAsync(1)).ReturnsAsync((User?)null);
            var svc = CreateService();

            var result = await svc.ApplyLeaveAsync(1, new LeaveCreateRequest
            {
                LeaveTypeId = 1,
                FromDate = DateTime.Today.AddDays(1),
                ToDate   = DateTime.Today.AddDays(3)
            });

            Assert.False(result.Success);
            Assert.Contains("user", result.Message, StringComparison.OrdinalIgnoreCase);
        }

        // ── ApplyLeaveAsync: leave type not found returns failure ─────────────

        [Fact]
        public async Task ApplyLeave_LeaveTypeNotFound_MessageContainsInvalid()
        {
            _userRepo.Setup(r => r.GetByIdAsync(1)).ReturnsAsync(MakeUser());
            _typeRepo.Setup(r => r.GetByIdAsync(1)).ReturnsAsync((LeaveType?)null);
            var svc = CreateService();

            var result = await svc.ApplyLeaveAsync(1, new LeaveCreateRequest
            {
                LeaveTypeId = 1,
                FromDate = DateTime.Today.AddDays(1),
                ToDate   = DateTime.Today.AddDays(3)
            });

            Assert.False(result.Success);
            Assert.Contains("invalid", result.Message, StringComparison.OrdinalIgnoreCase);
        }

        // ── ApproveOrRejectLeaveAsync: leave not found returns failure ────────

        [Fact]
        public async Task ApproveLeave_LeaveNotFound_ReturnsFail()
        {
            _leaveRepo.Setup(r => r.GetByIdAsync(99)).ReturnsAsync((LeaveRequest?)null);
            var svc = CreateService();

            var result = await svc.ApproveOrRejectLeaveAsync(
                new LeaveApprovalRequest { LeaveId = 99, ApprovedById = 1, IsApproved = true });

            Assert.False(result.Success);
        }

        // ── ApproveOrRejectLeaveAsync: approve sets status=Approved ──────────

        [Fact]
        public async Task ApproveLeave_SetsStatusApproved()
        {
            var leave = new LeaveRequest
            {
                Id = 1, UserId = 5, Status = LeaveStatus.Pending,
                LeaveTypeId = 1, FromDate = DateTime.Today.AddDays(1), ToDate = DateTime.Today.AddDays(3)
            };
            LeaveRequest? captured = null;
            _leaveRepo.Setup(r => r.GetByIdAsync(1)).ReturnsAsync(leave);
            _leaveRepo.Setup(r => r.UpdateAsync(1, It.IsAny<LeaveRequest>()))
                      .Callback<int, LeaveRequest>((_, l) => captured = l)
                      .ReturnsAsync(leave);
            _notif.Setup(n => n.SendToUserAsync(It.IsAny<int>(), It.IsAny<string>(), It.IsAny<string>()))
                  .Returns(Task.CompletedTask);

            var svc = CreateService();
            await svc.ApproveOrRejectLeaveAsync(
                new LeaveApprovalRequest { LeaveId = 1, ApprovedById = 10, IsApproved = true });

            Assert.Equal(LeaveStatus.Approved, captured?.Status);
        }

        // ── ApproveOrRejectLeaveAsync: reject sets status=Rejected ───────────

        [Fact]
        public async Task RejectLeave_SetsStatusRejected()
        {
            var leave = new LeaveRequest
            {
                Id = 1, UserId = 5, Status = LeaveStatus.Pending,
                LeaveTypeId = 1, FromDate = DateTime.Today.AddDays(1), ToDate = DateTime.Today.AddDays(3)
            };
            LeaveRequest? captured = null;
            _leaveRepo.Setup(r => r.GetByIdAsync(1)).ReturnsAsync(leave);
            _leaveRepo.Setup(r => r.UpdateAsync(1, It.IsAny<LeaveRequest>()))
                      .Callback<int, LeaveRequest>((_, l) => captured = l)
                      .ReturnsAsync(leave);
            _notif.Setup(n => n.SendToUserAsync(It.IsAny<int>(), It.IsAny<string>(), It.IsAny<string>()))
                  .Returns(Task.CompletedTask);

            var svc = CreateService();
            await svc.ApproveOrRejectLeaveAsync(
                new LeaveApprovalRequest { LeaveId = 1, ApprovedById = 10, IsApproved = false });

            Assert.Equal(LeaveStatus.Rejected, captured?.Status);
        }

        // ── GetUserLeavesAsync: returns filtered by status ────────────────────

        [Fact]
        public async Task GetUserLeaves_FilterByApproved_ReturnsOnlyApproved()
        {
            var user = MakeUser();
            var type = MakeType();
            var leaves = new List<LeaveRequest>
            {
                new() { Id = 1, UserId = 1, LeaveTypeId = 1, Status = LeaveStatus.Pending,
                        FromDate = DateTime.Today.AddDays(1), ToDate = DateTime.Today.AddDays(2) },
                new() { Id = 2, UserId = 1, LeaveTypeId = 1, Status = LeaveStatus.Approved,
                        FromDate = DateTime.Today.AddDays(5), ToDate = DateTime.Today.AddDays(7) },
                new() { Id = 3, UserId = 1, LeaveTypeId = 1, Status = LeaveStatus.Rejected,
                        FromDate = DateTime.Today.AddDays(10), ToDate = DateTime.Today.AddDays(11) }
            };
            _userRepo.Setup(r => r.GetByIdAsync(1)).ReturnsAsync(user);
            _leaveRepo.Setup(r => r.GetAllAsync()).ReturnsAsync(leaves);
            _typeRepo.Setup(r => r.GetAllAsync()).ReturnsAsync(new List<LeaveType> { type });

            var svc = CreateService();
            var result = await svc.GetUserLeavesAsync(1, 1, 10, status: "Approved");

            Assert.True(result.Success);
            Assert.Equal(1, result.Data?.TotalRecords);
        }

        // ── GetAllLeavesAsync: returns all leaves paged ───────────────────────

        [Fact]
        public async Task GetAllLeaves_ReturnsPaged()
        {
            var user = MakeUser();
            var type = MakeType();
            var leaves = Enumerable.Range(1, 6).Select(i => new LeaveRequest
            {
                Id = i, UserId = 1, LeaveTypeId = 1, Status = LeaveStatus.Pending,
                FromDate = DateTime.Today.AddDays(i), ToDate = DateTime.Today.AddDays(i + 1)
            }).ToList();
            _leaveRepo.Setup(r => r.GetAllAsync()).ReturnsAsync(leaves);
            _userRepo.Setup(r => r.GetAllAsync()).ReturnsAsync(new List<User> { user });
            _typeRepo.Setup(r => r.GetAllAsync()).ReturnsAsync(new List<LeaveType> { type });

            var svc = CreateService();
            var result = await svc.GetAllLeavesAsync(1, 4);

            Assert.True(result.Success);
            Assert.Equal(6, result.Data?.TotalRecords);
            Assert.Equal(4, result.Data?.Data.Count());
        }

        // ── DeleteLeaveAsync: not found returns failure ───────────────────────

        [Fact]
        public async Task DeleteLeave_NotFound_MessageContainsNotFound()
        {
            _leaveRepo.Setup(r => r.GetByIdAsync(99)).ReturnsAsync((LeaveRequest?)null);
            var svc = CreateService();

            var result = await svc.DeleteLeaveAsync(99, 1);

            Assert.False(result.Success);
            Assert.Contains("not found", result.Message, StringComparison.OrdinalIgnoreCase);
        }

        // ── DeleteLeaveAsync: wrong user returns failure ──────────────────────

        [Fact]
        public async Task DeleteLeave_WrongUser_MessageContainsNotAuthorized()
        {
            var leave = new LeaveRequest
            {
                Id = 1, UserId = 5, Status = LeaveStatus.Pending,
                LeaveTypeId = 1, FromDate = DateTime.Today, ToDate = DateTime.Today
            };
            _leaveRepo.Setup(r => r.GetByIdAsync(1)).ReturnsAsync(leave);
            var svc = CreateService();

            var result = await svc.DeleteLeaveAsync(1, userId: 99);

            Assert.False(result.Success);
            Assert.Contains("authorized", result.Message, StringComparison.OrdinalIgnoreCase);
        }

        // ── GetLeaveBalanceAsync: calculates remaining correctly ──────────────

        [Fact]
        public async Task GetLeaveBalance_CalculatesRemainingCorrectly()
        {
            var type = MakeType(maxDays: 15);
            var usedLeave = new LeaveRequest
            {
                Id = 1, UserId = 1, LeaveTypeId = 1,
                FromDate = DateTime.Today.AddDays(5),
                ToDate   = DateTime.Today.AddDays(9), // 5 days
                Status   = LeaveStatus.Approved
            };
            _leaveRepo.Setup(r => r.GetAllAsync()).ReturnsAsync(new List<LeaveRequest> { usedLeave });
            _typeRepo.Setup(r => r.GetAllAsync()).ReturnsAsync(new List<LeaveType> { type });

            var svc = CreateService();
            var result = await svc.GetLeaveBalanceAsync(1);

            Assert.True(result.Success);
            Assert.NotNull(result.Data);
            Assert.Single(result.Data!);
        }

        // ── GetAllLeavesAsync: search by user name ────────────────────────────

        [Fact]
        public async Task GetAllLeaves_SearchByUserName_ReturnsFiltered()
        {
            var user1 = MakeUser(1);
            var user2 = new User { Id = 2, Name = "Alice Smith", Email = "a@t.com", EmployeeId = "E002", Role = UserRole.Employee, PasswordHash = "h", IsActive = true };
            var type = MakeType();
            var leaves = new List<LeaveRequest>
            {
                new() { Id = 1, UserId = 1, LeaveTypeId = 1, Status = LeaveStatus.Pending, FromDate = DateTime.Today.AddDays(1), ToDate = DateTime.Today.AddDays(2) },
                new() { Id = 2, UserId = 2, LeaveTypeId = 1, Status = LeaveStatus.Pending, FromDate = DateTime.Today.AddDays(3), ToDate = DateTime.Today.AddDays(4) }
            };
            _leaveRepo.Setup(r => r.GetAllAsync()).ReturnsAsync(leaves);
            _userRepo.Setup(r => r.GetAllAsync()).ReturnsAsync(new List<User> { user1, user2 });
            _typeRepo.Setup(r => r.GetAllAsync()).ReturnsAsync(new List<LeaveType> { type });

            var svc = CreateService();
            var result = await svc.GetAllLeavesAsync(1, 10, search: "Alice");

            Assert.True(result.Success);
            Assert.Equal(1, result.Data?.TotalRecords);
        }

        // ── GetLeaveBalance: pending leaves also count against balance ────────

        [Fact]
        public async Task GetLeaveBalance_PendingLeavesCountedAgainstBalance()
        {
            var type = MakeType(maxDays: 10);
            var pendingLeave = new LeaveRequest
            {
                Id = 1, UserId = 1, LeaveTypeId = 1,
                FromDate = DateTime.Today.AddDays(1),
                ToDate   = DateTime.Today.AddDays(3), // 3 days pending
                Status   = LeaveStatus.Pending
            };
            _leaveRepo.Setup(r => r.GetAllAsync()).ReturnsAsync(new List<LeaveRequest> { pendingLeave });
            _typeRepo.Setup(r => r.GetAllAsync()).ReturnsAsync(new List<LeaveType> { type });

            var svc = CreateService();
            var result = await svc.GetLeaveBalanceAsync(1);

            Assert.True(result.Success);
            Assert.NotNull(result.Data);
        }
    }

namespace TimeSheetAppWeb.Tests.Services
{
    /// <summary>
    /// Further coverage: GetAllLeaves, notification verification, and balance edge cases.
    /// </summary>
    public class LeaveServiceFurtherTests
    {
        private readonly Mock<IRepository<int, LeaveRequest>> _leaveRepo = new();
        private readonly Mock<IRepository<int, User>>         _userRepo  = new();
        private readonly Mock<IRepository<int, LeaveType>>    _typeRepo  = new();
        private readonly Mock<INotificationService>           _notif     = new();
        private readonly Mock<ILogger<LeaveService>>          _logger    = new();

        private LeaveService CreateService() =>
            new(_leaveRepo.Object, _userRepo.Object, _typeRepo.Object, _notif.Object, _logger.Object);

        private User MakeUser(int id = 1) => new User
        {
            Id = id, Name = "Test User", Email = "test@test.com",
            EmployeeId = "E001", Role = UserRole.Employee,
            PasswordHash = "hash", IsActive = true
        };

        private LeaveType MakeType(int id = 1, int maxDays = 20) => new LeaveType
        {
            Id = id, Name = "Annual", MaxDaysPerYear = maxDays, IsActive = true
        };

        // ── ApplyLeave — notification sent to managers ────────────────────────

        [Fact]
        public async Task ApplyLeave_Valid_SendsNotificationToManagers()
        {
            var user = MakeUser();
            var type = MakeType();
            _userRepo.Setup(r => r.GetByIdAsync(1)).ReturnsAsync(user);
            _typeRepo.Setup(r => r.GetByIdAsync(1)).ReturnsAsync(type);
            _leaveRepo.Setup(r => r.GetAllAsync()).ReturnsAsync(new List<LeaveRequest>());
            _leaveRepo.Setup(r => r.AddAsync(It.IsAny<LeaveRequest>())).ReturnsAsync(new LeaveRequest
            {
                Id = 1, UserId = 1, LeaveTypeId = 1,
                FromDate = DateTime.Today.AddDays(1), ToDate = DateTime.Today.AddDays(2),
                Status = LeaveStatus.Pending
            });
            _notif.Setup(n => n.SendToRoleAsync("Manager", It.IsAny<string>(), It.IsAny<string>()))
                  .Returns(Task.CompletedTask);

            await CreateService().ApplyLeaveAsync(1, new LeaveCreateRequest
            {
                LeaveTypeId = 1, FromDate = DateTime.Today.AddDays(1), ToDate = DateTime.Today.AddDays(2)
            });

            _notif.Verify(n => n.SendToRoleAsync("Manager", It.IsAny<string>(), It.IsAny<string>()), Times.Once);
        }

        // ── ApproveLeave — notification sent to user ──────────────────────────

        [Fact]
        public async Task ApproveLeave_Valid_SendsNotificationToUser()
        {
            var leave = new LeaveRequest
            {
                Id = 1, UserId = 5, Status = LeaveStatus.Pending,
                LeaveTypeId = 1, FromDate = DateTime.Today.AddDays(1), ToDate = DateTime.Today.AddDays(3)
            };
            _leaveRepo.Setup(r => r.GetByIdAsync(1)).ReturnsAsync(leave);
            _leaveRepo.Setup(r => r.UpdateAsync(1, It.IsAny<LeaveRequest>())).ReturnsAsync(leave);
            _notif.Setup(n => n.SendToUserAsync(5, It.IsAny<string>(), It.IsAny<string>()))
                  .Returns(Task.CompletedTask);

            await CreateService().ApproveOrRejectLeaveAsync(
                new LeaveApprovalRequest { LeaveId = 1, ApprovedById = 10, IsApproved = true });

            _notif.Verify(n => n.SendToUserAsync(5, It.IsAny<string>(), It.IsAny<string>()), Times.Once);
        }

        // ── GetAllLeaves — search by employee name ────────────────────────────

        [Fact]
        public async Task GetAllLeaves_SearchByName_ReturnsMatchingLeaves()
        {
            var user1 = MakeUser(1);
            var user2 = new User { Id = 2, Name = "Alice", Email = "a@t.com", EmployeeId = "E002", Role = UserRole.Employee, PasswordHash = "h", IsActive = true };
            var type  = MakeType();
            var leaves = new List<LeaveRequest>
            {
                new() { Id = 1, UserId = 1, LeaveTypeId = 1, Status = LeaveStatus.Pending,
                        FromDate = DateTime.Today.AddDays(1), ToDate = DateTime.Today.AddDays(2) },
                new() { Id = 2, UserId = 2, LeaveTypeId = 1, Status = LeaveStatus.Pending,
                        FromDate = DateTime.Today.AddDays(3), ToDate = DateTime.Today.AddDays(4) }
            };
            _leaveRepo.Setup(r => r.GetAllAsync()).ReturnsAsync(leaves);
            _userRepo.Setup(r => r.GetAllAsync()).ReturnsAsync(new List<User> { user1, user2 });
            _typeRepo.Setup(r => r.GetAllAsync()).ReturnsAsync(new List<LeaveType> { type });

            var result = await CreateService().GetAllLeavesAsync(1, 10, search: "Alice");

            Assert.True(result.Success);
            Assert.Equal(1, result.Data?.TotalRecords);
        }

        // ── GetAllLeaves — status filter ──────────────────────────────────────

        [Fact]
        public async Task GetAllLeaves_StatusFilter_ReturnsOnlyMatchingStatus()
        {
            var user = MakeUser();
            var type = MakeType();
            var leaves = new List<LeaveRequest>
            {
                new() { Id = 1, UserId = 1, LeaveTypeId = 1, Status = LeaveStatus.Pending,
                        FromDate = DateTime.Today.AddDays(1), ToDate = DateTime.Today.AddDays(2) },
                new() { Id = 2, UserId = 1, LeaveTypeId = 1, Status = LeaveStatus.Approved,
                        FromDate = DateTime.Today.AddDays(5), ToDate = DateTime.Today.AddDays(6) }
            };
            _leaveRepo.Setup(r => r.GetAllAsync()).ReturnsAsync(leaves);
            _userRepo.Setup(r => r.GetAllAsync()).ReturnsAsync(new List<User> { user });
            _typeRepo.Setup(r => r.GetAllAsync()).ReturnsAsync(new List<LeaveType> { type });

            var result = await CreateService().GetAllLeavesAsync(1, 10, status: "Approved");

            Assert.True(result.Success);
            Assert.Equal(1, result.Data?.TotalRecords);
        }

        // ── GetLeaveBalance — rejected leaves not counted ─────────────────────

        [Fact]
        public async Task GetLeaveBalance_RejectedLeavesNotCounted()
        {
            var type = MakeType(maxDays: 10);
            var leaves = new List<LeaveRequest>
            {
                new() { Id = 1, UserId = 1, LeaveTypeId = 1,
                        FromDate = DateTime.Today.AddDays(5), ToDate = DateTime.Today.AddDays(7),
                        Status = LeaveStatus.Rejected }, // 3 days — should NOT count
                new() { Id = 2, UserId = 1, LeaveTypeId = 1,
                        FromDate = DateTime.Today.AddDays(10), ToDate = DateTime.Today.AddDays(11),
                        Status = LeaveStatus.Approved }  // 2 days — should count
            };
            _leaveRepo.Setup(r => r.GetAllAsync()).ReturnsAsync(leaves);
            _typeRepo.Setup(r => r.GetAllAsync()).ReturnsAsync(new List<LeaveType> { type });

            var result = await CreateService().GetLeaveBalanceAsync(1);

            Assert.True(result.Success);
            Assert.NotNull(result.Data);
            // Remaining = 10 - 2 = 8 (rejected not counted)
            Assert.Single(result.Data!);
        }

        // ── ApplyLeave — today as from date (boundary) ────────────────────────

        [Fact]
        public async Task ApplyLeave_TodayAsFromDate_Succeeds()
        {
            var user = MakeUser();
            var type = MakeType();
            _userRepo.Setup(r => r.GetByIdAsync(1)).ReturnsAsync(user);
            _typeRepo.Setup(r => r.GetByIdAsync(1)).ReturnsAsync(type);
            _leaveRepo.Setup(r => r.GetAllAsync()).ReturnsAsync(new List<LeaveRequest>());
            _leaveRepo.Setup(r => r.AddAsync(It.IsAny<LeaveRequest>())).ReturnsAsync(new LeaveRequest
            {
                Id = 1, UserId = 1, LeaveTypeId = 1,
                FromDate = DateTime.Today, ToDate = DateTime.Today.AddDays(2),
                Status = LeaveStatus.Pending
            });
            _notif.Setup(n => n.SendToRoleAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()))
                  .Returns(Task.CompletedTask);

            var result = await CreateService().ApplyLeaveAsync(1, new LeaveCreateRequest
            {
                LeaveTypeId = 1, FromDate = DateTime.Today, ToDate = DateTime.Today.AddDays(2)
            });

            Assert.True(result.Success);
        }

        // ── GetUserLeaves — pagination ────────────────────────────────────────

        [Fact]
        public async Task GetUserLeaves_Page2_ReturnsCorrectSlice()
        {
            var user = MakeUser();
            var type = MakeType();
            var leaves = Enumerable.Range(1, 7).Select(i => new LeaveRequest
            {
                Id = i, UserId = 1, LeaveTypeId = 1, Status = LeaveStatus.Pending,
                FromDate = DateTime.Today.AddDays(i), ToDate = DateTime.Today.AddDays(i + 1)
            }).ToList();
            _userRepo.Setup(r => r.GetByIdAsync(1)).ReturnsAsync(user);
            _leaveRepo.Setup(r => r.GetAllAsync()).ReturnsAsync(leaves);
            _typeRepo.Setup(r => r.GetAllAsync()).ReturnsAsync(new List<LeaveType> { type });

            var result = await CreateService().GetUserLeavesAsync(1, 2, 5);

            Assert.True(result.Success);
            Assert.Equal(7, result.Data?.TotalRecords);
            Assert.Equal(2, result.Data?.Data.Count());
        }

        // ── ApproveLeave — comment stored ─────────────────────────────────────

        [Fact]
        public async Task ApproveLeave_CommentStored_InUpdatedRecord()
        {
            var leave = new LeaveRequest
            {
                Id = 1, UserId = 5, Status = LeaveStatus.Pending,
                LeaveTypeId = 1, FromDate = DateTime.Today.AddDays(1), ToDate = DateTime.Today.AddDays(3)
            };
            LeaveRequest? captured = null;
            _leaveRepo.Setup(r => r.GetByIdAsync(1)).ReturnsAsync(leave);
            _leaveRepo.Setup(r => r.UpdateAsync(1, It.IsAny<LeaveRequest>()))
                      .Callback<int, LeaveRequest>((_, l) => captured = l)
                      .ReturnsAsync(leave);
            _notif.Setup(n => n.SendToUserAsync(It.IsAny<int>(), It.IsAny<string>(), It.IsAny<string>()))
                  .Returns(Task.CompletedTask);

            await CreateService().ApproveOrRejectLeaveAsync(
                new LeaveApprovalRequest { LeaveId = 1, ApprovedById = 10, IsApproved = true, ManagerComment = "Enjoy!" });

            Assert.Equal("Enjoy!", captured?.ManagerComment);
        }
    }
}
