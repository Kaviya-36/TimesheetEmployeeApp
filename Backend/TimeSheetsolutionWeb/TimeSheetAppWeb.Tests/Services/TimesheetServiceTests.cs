using Microsoft.Extensions.Logging;
using Moq;
using TimeSheetAppWeb.Interface;
using TimeSheetAppWeb.Model;
using TimeSheetAppWeb.Model.common;
using TimeSheetAppWeb.Model.DTOs;
using TimeSheetAppWeb.Services;
using Xunit;

namespace TimeSheetAppWeb.Tests.Services
{
    /// <summary>
    /// Unit tests for <see cref="TimesheetService"/> — core submit/approve/delete flows.
    /// </summary>
    public class TimesheetServiceTests
    {
        // ── Mocks ──────────────────────────────────────────────────────────────

        private readonly Mock<IRepository<int, Timesheet>> _tsRepo   = new();
        private readonly Mock<IRepository<int, User>>      _userRepo = new();
        private readonly Mock<IRepository<int, Project>>   _prjRepo  = new();
        private readonly Mock<IProjectService>             _prjSvc   = new();
        private readonly Mock<IAttendanceService>          _attSvc   = new();
        private readonly Mock<INotificationService>        _notif    = new();
        private readonly Mock<ILogger<TimesheetService>>   _logger   = new();

        // ── Helpers ────────────────────────────────────────────────────────────

        private TimesheetService CreateService() =>
            new(_tsRepo.Object, _userRepo.Object, _prjRepo.Object,
                _prjSvc.Object, _attSvc.Object, _notif.Object, _logger.Object);

        private static User MakeUser(int id = 1) => new()
        {
            Id           = id,
            Name         = "Test User",
            Email        = "test@example.com",
            EmployeeId   = "E001",
            Role         = UserRole.Employee,
            PasswordHash = "hashed",
            IsActive     = true
        };

        private static Project MakeProject(int id = 1) => new()
        {
            Id          = id,
            ProjectName = "Test Project",
            StartDate   = DateTime.Today
        };

        private static TimesheetWeeklyRequest MakeWeeklyRequest(
            int projectId = 1,
            string projectName = "Test Project",
            double hours = 8) =>
            new()
            {
                Entries = new List<TimesheetWeeklyEntry>
                {
                    new()
                    {
                        ProjectId   = projectId,
                        ProjectName = projectName,
                        WorkDate    = DateTime.Today.ToString("yyyy-MM-dd"),
                        Hours       = hours
                    }
                },
                Submit = true
            };

        // ── SubmitWeeklyAsync ──────────────────────────────────────────────────

        [Fact]
        public async Task SubmitWeekly_UserNotFound_ReturnsFail()
        {
            _userRepo.Setup(r => r.GetByIdAsync(1)).ReturnsAsync((User?)null);

            var result = await CreateService().SubmitWeeklyAsync(1, MakeWeeklyRequest());

            Assert.False(result.Success);
        }

        [Fact]
        public async Task SubmitWeekly_ZeroHours_SkipsEntry()
        {
            _userRepo.Setup(r => r.GetByIdAsync(1)).ReturnsAsync(MakeUser());
            _prjRepo.Setup(r => r.GetAllAsync()).ReturnsAsync(new List<Project>());
            _tsRepo.Setup(r => r.GetAllAsync()).ReturnsAsync(new List<Timesheet>());

            var result = await CreateService().SubmitWeeklyAsync(1, MakeWeeklyRequest(hours: 0));

            Assert.True(result.Success);
            Assert.Equal(0, result.Data?.Saved);
            Assert.Equal(1, result.Data?.Skipped);
        }

        [Fact]
        public async Task SubmitWeekly_NewEntry_SavesSuccessfully()
        {
            var project = MakeProject();
            _userRepo.Setup(r => r.GetByIdAsync(1)).ReturnsAsync(MakeUser());
            _prjRepo.Setup(r => r.GetByIdAsync(1)).ReturnsAsync(project);
            _prjRepo.Setup(r => r.GetAllAsync()).ReturnsAsync(new List<Project> { project });
            _tsRepo.Setup(r => r.GetAllAsync()).ReturnsAsync(new List<Timesheet>());
            _tsRepo.Setup(r => r.AddAsync(It.IsAny<Timesheet>())).ReturnsAsync(new Timesheet { Id = 1 });
            _notif.Setup(n => n.SendToRoleAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()))
                  .Returns(Task.CompletedTask);

            var result = await CreateService().SubmitWeeklyAsync(1, MakeWeeklyRequest());

            Assert.True(result.Success);
            Assert.Equal(1, result.Data?.Saved);
        }

        [Fact]
        public async Task SubmitWeekly_ApprovedEntry_SkipsIt()
        {
            var project  = MakeProject();
            var approved = new Timesheet
            {
                Id        = 10,
                UserId    = 1,
                ProjectId = 1,
                WorkDate  = DateTime.Today,
                Status    = TimesheetStatus.Approved,
                StartTime = TimeSpan.FromHours(9),
                EndTime   = TimeSpan.FromHours(17),
                TotalHours = 8
            };
            _userRepo.Setup(r => r.GetByIdAsync(1)).ReturnsAsync(MakeUser());
            _prjRepo.Setup(r => r.GetByIdAsync(1)).ReturnsAsync(project);
            _prjRepo.Setup(r => r.GetAllAsync()).ReturnsAsync(new List<Project> { project });
            _tsRepo.Setup(r => r.GetAllAsync()).ReturnsAsync(new List<Timesheet> { approved });

            var result = await CreateService().SubmitWeeklyAsync(1, MakeWeeklyRequest());

            Assert.True(result.Success);
            Assert.Equal(1, result.Data?.AlreadyApproved);
        }

        // ── ApproveOrRejectTimesheetAsync ──────────────────────────────────────

        [Fact]
        public async Task ApproveTimesheet_NotFound_ReturnsFail()
        {
            _tsRepo.Setup(r => r.GetByIdAsync(99)).ReturnsAsync((Timesheet?)null);

            var result = await CreateService().ApproveOrRejectTimesheetAsync(
                new TimesheetApprovalRequest { TimesheetId = 99, ApprovedById = 1, IsApproved = true });

            Assert.False(result.Success);
        }

        [Fact]
        public async Task ApproveTimesheet_Valid_ReturnsSuccess()
        {
            var ts = new Timesheet
            {
                Id        = 1,
                UserId    = 5,
                ProjectId = 1,
                Status    = TimesheetStatus.Pending,
                WorkDate  = DateTime.Today,
                StartTime = TimeSpan.FromHours(9),
                EndTime   = TimeSpan.FromHours(17),
                TotalHours = 8
            };
            _tsRepo.Setup(r => r.GetByIdAsync(1)).ReturnsAsync(ts);
            _tsRepo.Setup(r => r.UpdateAsync(1, It.IsAny<Timesheet>())).ReturnsAsync(ts);
            _notif.Setup(n => n.SendToUserAsync(It.IsAny<int>(), It.IsAny<string>(), It.IsAny<string>()))
                  .Returns(Task.CompletedTask);

            var result = await CreateService().ApproveOrRejectTimesheetAsync(
                new TimesheetApprovalRequest
                {
                    TimesheetId    = 1,
                    ApprovedById   = 10,
                    IsApproved     = true,
                    ManagerComment = "Good work"
                });

            Assert.True(result.Success);
            Assert.True(result.Data);
        }

        // ── DeleteTimesheetAsync ───────────────────────────────────────────────

        [Fact]
        public async Task DeleteTimesheet_Approved_ReturnsFail()
        {
            var ts = new Timesheet
            {
                Id        = 1,
                Status    = TimesheetStatus.Approved,
                WorkDate  = DateTime.Today,
                StartTime = TimeSpan.Zero,
                EndTime   = TimeSpan.Zero
            };
            _tsRepo.Setup(r => r.GetByIdAsync(1)).ReturnsAsync(ts);

            var result = await CreateService().DeleteTimesheetAsync(1);

            Assert.False(result.Success);
        }

        [Fact]
        public async Task DeleteTimesheet_Pending_ReturnsSuccess()
        {
            var ts = new Timesheet
            {
                Id        = 1,
                Status    = TimesheetStatus.Pending,
                WorkDate  = DateTime.Today,
                StartTime = TimeSpan.Zero,
                EndTime   = TimeSpan.Zero
            };
            _tsRepo.Setup(r => r.GetByIdAsync(1)).ReturnsAsync(ts);
            _tsRepo.Setup(r => r.DeleteAsync(1)).ReturnsAsync(ts);

            var result = await CreateService().DeleteTimesheetAsync(1);

            Assert.True(result.Success);
        }
    }

    /// <summary>
    /// Additional coverage tests for <see cref="TimesheetService"/> —
    /// update, pagination, filtering, and rejection flows.
    /// </summary>
    public class TimesheetServiceCoverageTests
    {
        // ── Mocks ──────────────────────────────────────────────────────────────

        private readonly Mock<IRepository<int, Timesheet>> _tsRepo   = new();
        private readonly Mock<IRepository<int, User>>      _userRepo = new();
        private readonly Mock<IRepository<int, Project>>   _prjRepo  = new();
        private readonly Mock<IProjectService>             _prjSvc   = new();
        private readonly Mock<IAttendanceService>          _attSvc   = new();
        private readonly Mock<INotificationService>        _notif    = new();
        private readonly Mock<ILogger<TimesheetService>>   _logger   = new();

        // ── Helpers ────────────────────────────────────────────────────────────

        private TimesheetService CreateService() =>
            new(_tsRepo.Object, _userRepo.Object, _prjRepo.Object,
                _prjSvc.Object, _attSvc.Object, _notif.Object, _logger.Object);

        private static User MakeUser(int id = 1) => new()
        {
            Id           = id,
            Name         = "Test User",
            Email        = "test@example.com",
            EmployeeId   = "E001",
            Role         = UserRole.Employee,
            PasswordHash = "hashed",
            IsActive     = true
        };

        private static Project MakeProject(int id = 1) => new()
        {
            Id          = id,
            ProjectName = "Test Project",
            StartDate   = DateTime.Today
        };

        private static Timesheet MakeTs(
            int id = 1,
            int userId = 1,
            TimesheetStatus status = TimesheetStatus.Pending) =>
            new()
            {
                Id          = id,
                UserId      = userId,
                ProjectId   = 1,
                ProjectName = "Test Project",
                WorkDate    = DateTime.Today,
                StartTime   = TimeSpan.FromHours(9),
                EndTime     = TimeSpan.FromHours(17),
                TotalHours  = 8,
                Status      = status
            };

        // ── UpdateTimesheetAsync ───────────────────────────────────────────────

        [Fact]
        public async Task UpdateTimesheet_NotFound_ReturnsFail()
        {
            _tsRepo.Setup(r => r.GetByIdAsync(99)).ReturnsAsync((Timesheet?)null);

            var result = await CreateService().UpdateTimesheetAsync(
                99, new TimesheetUpdateRequest { WorkDate = DateTime.Today });

            Assert.False(result.Success);
        }

        [Fact]
        public async Task UpdateTimesheet_Approved_ReturnsFail()
        {
            _tsRepo.Setup(r => r.GetByIdAsync(1)).ReturnsAsync(MakeTs(status: TimesheetStatus.Approved));

            var result = await CreateService().UpdateTimesheetAsync(
                1, new TimesheetUpdateRequest { WorkDate = DateTime.Today });

            Assert.False(result.Success);
            Assert.Contains("approved", result.Message, StringComparison.OrdinalIgnoreCase);
        }

        [Fact]
        public async Task UpdateTimesheet_Valid_ReturnsSuccess()
        {
            var ts      = MakeTs();
            var project = MakeProject();
            _tsRepo.Setup(r => r.GetByIdAsync(1)).ReturnsAsync(ts);
            _tsRepo.Setup(r => r.UpdateAsync(1, It.IsAny<Timesheet>())).ReturnsAsync(ts);
            _userRepo.Setup(r => r.GetByIdAsync(1)).ReturnsAsync(MakeUser());
            _prjRepo.Setup(r => r.GetByIdAsync(1)).ReturnsAsync(project);

            var result = await CreateService().UpdateTimesheetAsync(1, new TimesheetUpdateRequest
            {
                WorkDate  = DateTime.Today,
                StartTime = TimeSpan.FromHours(9),
                EndTime   = TimeSpan.FromHours(17),
                BreakTime = TimeSpan.FromHours(1)
            });

            Assert.True(result.Success);
        }

        // ── GetUserTimesheetsAsync ─────────────────────────────────────────────

        [Fact]
        public async Task GetUserTimesheets_ReturnsPagedResultsForUser()
        {
            var project    = MakeProject();
            var timesheets = new List<Timesheet> { MakeTs(1, 1), MakeTs(2, 1), MakeTs(3, 2) };
            _tsRepo.Setup(r => r.GetAllAsync()).ReturnsAsync(timesheets);
            _userRepo.Setup(r => r.GetByIdAsync(1)).ReturnsAsync(MakeUser());
            _prjRepo.Setup(r => r.GetByIdAsync(1)).ReturnsAsync(project);

            var result = await CreateService().GetUserTimesheetsAsync(
                1, new PaginationParams { PageNumber = 1, PageSize = 10 });

            Assert.True(result.Success);
            Assert.Equal(2, result.Data?.TotalRecords);
        }

        [Fact]
        public async Task GetUserTimesheets_WithStatusFilter_ReturnsFilteredResults()
        {
            var project    = MakeProject();
            var timesheets = new List<Timesheet>
            {
                MakeTs(1, 1, TimesheetStatus.Pending),
                MakeTs(2, 1, TimesheetStatus.Approved)
            };
            _tsRepo.Setup(r => r.GetAllAsync()).ReturnsAsync(timesheets);
            _userRepo.Setup(r => r.GetByIdAsync(1)).ReturnsAsync(MakeUser());
            _prjRepo.Setup(r => r.GetByIdAsync(1)).ReturnsAsync(project);

            var result = await CreateService().GetUserTimesheetsAsync(
                1, new PaginationParams { PageNumber = 1, PageSize = 10, Status = "Approved" });

            Assert.True(result.Success);
            Assert.Equal(1, result.Data?.TotalRecords);
        }

        // ── GetAllTimesheetsAsync ──────────────────────────────────────────────

        [Fact]
        public async Task GetAllTimesheets_ReturnsPaginatedResults()
        {
            var user       = MakeUser();
            var project    = MakeProject();
            var timesheets = Enumerable.Range(1, 5).Select(i => MakeTs(i, 1)).ToList();
            _tsRepo.Setup(r => r.GetAllAsync()).ReturnsAsync(timesheets);
            _userRepo.Setup(r => r.GetAllAsync()).ReturnsAsync(new List<User> { user });
            _userRepo.Setup(r => r.GetByIdAsync(1)).ReturnsAsync(user);
            _prjRepo.Setup(r => r.GetByIdAsync(1)).ReturnsAsync(project);

            var result = await CreateService().GetAllTimesheetsAsync(
                new PaginationParams { PageNumber = 1, PageSize = 3 });

            Assert.True(result.Success);
            Assert.Equal(5, result.Data?.TotalRecords);
            Assert.Equal(3, result.Data?.Data.Count());
        }

        [Fact]
        public async Task GetAllTimesheets_SearchByEmployeeName_ReturnsFilteredResults()
        {
            var user       = MakeUser();
            var project    = MakeProject();
            var timesheets = new List<Timesheet> { MakeTs(1, 1) };
            _tsRepo.Setup(r => r.GetAllAsync()).ReturnsAsync(timesheets);
            _userRepo.Setup(r => r.GetAllAsync()).ReturnsAsync(new List<User> { user });
            _userRepo.Setup(r => r.GetByIdAsync(1)).ReturnsAsync(user);
            _prjRepo.Setup(r => r.GetByIdAsync(1)).ReturnsAsync(project);

            var result = await CreateService().GetAllTimesheetsAsync(
                new PaginationParams { PageNumber = 1, PageSize = 10, Search = "Test" });

            Assert.True(result.Success);
            Assert.Equal(1, result.Data?.TotalRecords);
        }

        // ── ApproveOrRejectTimesheetAsync ──────────────────────────────────────

        [Fact]
        public async Task RejectTimesheet_Valid_ReturnsSuccess()
        {
            var ts = MakeTs();
            _tsRepo.Setup(r => r.GetByIdAsync(1)).ReturnsAsync(ts);
            _tsRepo.Setup(r => r.UpdateAsync(1, It.IsAny<Timesheet>())).ReturnsAsync(ts);
            _notif.Setup(n => n.SendToUserAsync(It.IsAny<int>(), It.IsAny<string>(), It.IsAny<string>()))
                  .Returns(Task.CompletedTask);

            var result = await CreateService().ApproveOrRejectTimesheetAsync(
                new TimesheetApprovalRequest
                {
                    TimesheetId    = 1,
                    ApprovedById   = 10,
                    IsApproved     = false,
                    ManagerComment = "Insufficient detail"
                });

            Assert.True(result.Success);
        }
    }
}
