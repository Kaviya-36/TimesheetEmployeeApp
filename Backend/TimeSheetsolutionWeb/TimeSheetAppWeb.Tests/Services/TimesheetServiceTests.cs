using Microsoft.Extensions.Logging;
using Moq;
using TimeSheetAppWeb.Interface;
using TimeSheetAppWeb.Model;
using TimeSheetAppWeb.Model.DTOs;
using TimeSheetAppWeb.Services;
using Xunit;

namespace TimeSheetAppWeb.Tests.Services
{
    public class TimesheetServiceTests
    {
        private readonly Mock<IRepository<int, Timesheet>> _tsRepo   = new();
        private readonly Mock<IRepository<int, User>>      _userRepo = new();
        private readonly Mock<IRepository<int, Project>>   _prjRepo  = new();
        private readonly Mock<IProjectService>             _prjSvc   = new();
        private readonly Mock<IAttendanceService>          _attSvc   = new();
        private readonly Mock<INotificationService>        _notif    = new();
        private readonly Mock<ILogger<TimesheetService>>   _logger   = new();

        private TimesheetService CreateService() =>
            new(_tsRepo.Object, _userRepo.Object, _prjRepo.Object,
                _prjSvc.Object, _attSvc.Object, _notif.Object, _logger.Object);

        private User MakeUser(int id = 1) => new User
        {
            Id = id, Name = "Test", Email = "t@t.com",
            EmployeeId = "E001", Role = UserRole.Employee,
            PasswordHash = "h", IsActive = true
        };

        private Project MakeProject(int id = 1) => new Project
        {
            Id = id, ProjectName = "Test Project",
            StartDate = DateTime.Today
        };

        // ── SubmitWeeklyAsync ──────────────────────────────────────────────────

        [Fact]
        public async Task SubmitWeekly_UserNotFound_ReturnsFail()
        {
            _userRepo.Setup(r => r.GetByIdAsync(1)).ReturnsAsync((User?)null);
            var svc = CreateService();

            var result = await svc.SubmitWeeklyAsync(1, new TimesheetWeeklyRequest
            {
                Entries = new List<TimesheetWeeklyEntry>
                {
                    new() { ProjectId = 1, ProjectName = "P", WorkDate = DateTime.Today.ToString("yyyy-MM-dd"), Hours = 8 }
                },
                Submit = true
            });

            Assert.False(result.Success);
        }

        [Fact]
        public async Task SubmitWeekly_ZeroHours_SkipsEntry()
        {
            _userRepo.Setup(r => r.GetByIdAsync(1)).ReturnsAsync(MakeUser());
            _prjRepo.Setup(r => r.GetAllAsync()).ReturnsAsync(new List<Project>());
            _tsRepo.Setup(r => r.GetAllAsync()).ReturnsAsync(new List<Timesheet>());

            var svc = CreateService();
            var result = await svc.SubmitWeeklyAsync(1, new TimesheetWeeklyRequest
            {
                Entries = new List<TimesheetWeeklyEntry>
                {
                    new() { ProjectId = 1, ProjectName = "P", WorkDate = DateTime.Today.ToString("yyyy-MM-dd"), Hours = 0 }
                },
                Submit = true
            });

            Assert.True(result.Success);
            Assert.Equal(0, result.Data?.Saved);
            Assert.Equal(1, result.Data?.Skipped);
        }

        [Fact]
        public async Task SubmitWeekly_NewEntry_SavesSuccessfully()
        {
            var user    = MakeUser();
            var project = MakeProject();
            _userRepo.Setup(r => r.GetByIdAsync(1)).ReturnsAsync(user);
            _prjRepo.Setup(r => r.GetByIdAsync(1)).ReturnsAsync(project);
            _prjRepo.Setup(r => r.GetAllAsync()).ReturnsAsync(new List<Project> { project });
            _tsRepo.Setup(r => r.GetAllAsync()).ReturnsAsync(new List<Timesheet>());
            _tsRepo.Setup(r => r.AddAsync(It.IsAny<Timesheet>())).ReturnsAsync(new Timesheet { Id = 1 });
            _notif.Setup(n => n.SendToRoleAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()))
                  .Returns(Task.CompletedTask);

            var svc = CreateService();
            var result = await svc.SubmitWeeklyAsync(1, new TimesheetWeeklyRequest
            {
                Entries = new List<TimesheetWeeklyEntry>
                {
                    new() { ProjectId = 1, ProjectName = "Test Project", WorkDate = DateTime.Today.ToString("yyyy-MM-dd"), Hours = 8 }
                },
                Submit = true
            });

            Assert.True(result.Success);
            Assert.Equal(1, result.Data?.Saved);
        }

        [Fact]
        public async Task SubmitWeekly_ApprovedEntry_SkipsIt()
        {
            var user    = MakeUser();
            var project = MakeProject();
            var existing = new Timesheet
            {
                Id = 10, UserId = 1, ProjectId = 1,
                WorkDate = DateTime.Today, Status = TimesheetStatus.Approved,
                StartTime = TimeSpan.FromHours(9), EndTime = TimeSpan.FromHours(17), TotalHours = 8
            };
            _userRepo.Setup(r => r.GetByIdAsync(1)).ReturnsAsync(user);
            _prjRepo.Setup(r => r.GetByIdAsync(1)).ReturnsAsync(project);
            _prjRepo.Setup(r => r.GetAllAsync()).ReturnsAsync(new List<Project> { project });
            _tsRepo.Setup(r => r.GetAllAsync()).ReturnsAsync(new List<Timesheet> { existing });

            var svc = CreateService();
            var result = await svc.SubmitWeeklyAsync(1, new TimesheetWeeklyRequest
            {
                Entries = new List<TimesheetWeeklyEntry>
                {
                    new() { ProjectId = 1, ProjectName = "Test Project", WorkDate = DateTime.Today.ToString("yyyy-MM-dd"), Hours = 8 }
                },
                Submit = true
            });

            Assert.True(result.Success);
            Assert.Equal(1, result.Data?.AlreadyApproved);
        }

        // ── ApproveOrRejectTimesheetAsync ──────────────────────────────────────

        [Fact]
        public async Task ApproveTimesheet_NotFound_ReturnsFail()
        {
            _tsRepo.Setup(r => r.GetByIdAsync(99)).ReturnsAsync((Timesheet?)null);
            var svc = CreateService();

            var result = await svc.ApproveOrRejectTimesheetAsync(new TimesheetApprovalRequest
            {
                TimesheetId = 99, ApprovedById = 1, IsApproved = true
            });

            Assert.False(result.Success);
        }

        [Fact]
        public async Task ApproveTimesheet_Valid_UpdatesStatus()
        {
            var ts = new Timesheet
            {
                Id = 1, UserId = 5, ProjectId = 1, Status = TimesheetStatus.Pending,
                WorkDate = DateTime.Today, StartTime = TimeSpan.FromHours(9),
                EndTime = TimeSpan.FromHours(17), TotalHours = 8
            };
            _tsRepo.Setup(r => r.GetByIdAsync(1)).ReturnsAsync(ts);
            _tsRepo.Setup(r => r.UpdateAsync(1, It.IsAny<Timesheet>())).ReturnsAsync(ts);
            _notif.Setup(n => n.SendToUserAsync(It.IsAny<int>(), It.IsAny<string>(), It.IsAny<string>()))
                  .Returns(Task.CompletedTask);

            var svc = CreateService();
            var result = await svc.ApproveOrRejectTimesheetAsync(new TimesheetApprovalRequest
            {
                TimesheetId = 1, ApprovedById = 10, IsApproved = true, ManagerComment = "Good work"
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
                Id = 1, Status = TimesheetStatus.Approved,
                WorkDate = DateTime.Today, StartTime = TimeSpan.Zero, EndTime = TimeSpan.Zero
            };
            _tsRepo.Setup(r => r.GetByIdAsync(1)).ReturnsAsync(ts);

            var svc = CreateService();
            var result = await svc.DeleteTimesheetAsync(1);

            Assert.False(result.Success);
        }

        [Fact]
        public async Task DeleteTimesheet_Pending_ReturnsSuccess()
        {
            var ts = new Timesheet
            {
                Id = 1, Status = TimesheetStatus.Pending,
                WorkDate = DateTime.Today, StartTime = TimeSpan.Zero, EndTime = TimeSpan.Zero
            };
            _tsRepo.Setup(r => r.GetByIdAsync(1)).ReturnsAsync(ts);
            _tsRepo.Setup(r => r.DeleteAsync(1)).ReturnsAsync(ts);

            var svc = CreateService();
            var result = await svc.DeleteTimesheetAsync(1);

            Assert.True(result.Success);
        }
    }
}
