using Microsoft.Extensions.Logging;
using Moq;
using TimeSheetAppWeb.Interface;
using TimeSheetAppWeb.Model;
using TimeSheetAppWeb.Model.DTOs;
using TimeSheetAppWeb.Services;
using Xunit;

namespace TimeSheetAppWeb.Tests.Services
{
    public class AnalyticsServiceTests
    {
        private readonly Mock<IRepository<int, Timesheet>> _tsRepo = new();
        private readonly Mock<ILogger<AnalyticsService>>   _logger = new();

        private AnalyticsService CreateService() =>
            new(_tsRepo.Object, _logger.Object);

        private Timesheet MakeTs(int id, int userId, int projectId, double hours, TimesheetStatus status = TimesheetStatus.Approved) =>
            new Timesheet
            {
                Id = id, UserId = userId, ProjectId = projectId,
                ProjectName = $"Project {projectId}",
                WorkDate = DateTime.Today, TotalHours = hours,
                Status = status,
                StartTime = TimeSpan.FromHours(9),
                EndTime   = TimeSpan.FromHours(9 + hours),
                User = new User
                {
                    Id = userId, Name = $"User {userId}", Email = $"u{userId}@t.com",
                    EmployeeId = $"E{userId:000}", Role = UserRole.Employee,
                    PasswordHash = "h", IsActive = true
                }
            };

        // ── GetTimesheetAnalyticsAsync ─────────────────────────────────────────

        [Fact]
        public async Task GetAnalytics_NoFilter_ReturnsAllApprovedAndPending()
        {
            var timesheets = new List<Timesheet>
            {
                MakeTs(1, 1, 1, 8, TimesheetStatus.Approved),
                MakeTs(2, 2, 2, 4, TimesheetStatus.Pending),
                MakeTs(3, 3, 3, 6, TimesheetStatus.Rejected)  // should be excluded
            };
            _tsRepo.Setup(r => r.GetAllAsync()).ReturnsAsync(timesheets);

            var svc = CreateService();
            var result = await svc.GetTimesheetAnalyticsAsync(new AnalyticsRequest());

            // Rejected excluded — 2 groups remain (user1/proj1 and user2/proj2)
            var list = result.ToList();
            Assert.Equal(2, list.Count);
        }

        [Fact]
        public async Task GetAnalytics_FilterByUser_ReturnsOnlyThatUser()
        {
            var timesheets = new List<Timesheet>
            {
                MakeTs(1, 1, 1, 8),
                MakeTs(2, 2, 1, 6)
            };
            _tsRepo.Setup(r => r.GetAllAsync()).ReturnsAsync(timesheets);

            var svc = CreateService();
            var result = await svc.GetTimesheetAnalyticsAsync(new AnalyticsRequest { UserId = 1 });

            Assert.Single(result);
            Assert.Equal(1, result.First().UserId);
        }

        [Fact]
        public async Task GetAnalytics_FilterByProject_ReturnsOnlyThatProject()
        {
            var timesheets = new List<Timesheet>
            {
                MakeTs(1, 1, 1, 8),
                MakeTs(2, 1, 2, 6)
            };
            _tsRepo.Setup(r => r.GetAllAsync()).ReturnsAsync(timesheets);

            var svc = CreateService();
            var result = await svc.GetTimesheetAnalyticsAsync(new AnalyticsRequest { ProjectId = 2 });

            Assert.Single(result);
            Assert.Equal(2, result.First().ProjectId);
        }

        [Fact]
        public async Task GetAnalytics_TotalHoursCalculatedCorrectly()
        {
            var timesheets = new List<Timesheet>
            {
                MakeTs(1, 1, 1, 8),
                MakeTs(2, 1, 1, 4)  // same user+project, different day
            };
            // Set different dates so they're distinct days
            timesheets[1].WorkDate = DateTime.Today.AddDays(-1);
            _tsRepo.Setup(r => r.GetAllAsync()).ReturnsAsync(timesheets);

            var svc = CreateService();
            var result = await svc.GetTimesheetAnalyticsAsync(new AnalyticsRequest { UserId = 1, ProjectId = 1 });

            Assert.Single(result);
            Assert.Equal(12m, result.First().TotalHoursWorked);
            Assert.Equal(2, result.First().DaysWorked);
            Assert.Equal(6m, result.First().AverageHoursPerDay);
        }

        // ── GetDashboardSummaryAsync ───────────────────────────────────────────

        [Fact]
        public async Task GetDashboardSummary_EmptyData_ReturnsZeros()
        {
            _tsRepo.Setup(r => r.GetAllAsync()).ReturnsAsync(new List<Timesheet>());

            var svc = CreateService();
            var result = await svc.GetDashboardSummaryAsync(new AnalyticsRequest());

            Assert.Equal(0m, result.TotalHours);
            Assert.Equal(0, result.TotalUsers);
            Assert.Equal(0, result.TotalProjects);
        }

        [Fact]
        public async Task GetDashboardSummary_CalculatesCorrectly()
        {
            var timesheets = new List<Timesheet>
            {
                MakeTs(1, 1, 1, 8),
                MakeTs(2, 2, 1, 6),
                MakeTs(3, 1, 2, 4)
            };
            _tsRepo.Setup(r => r.GetAllAsync()).ReturnsAsync(timesheets);

            var svc = CreateService();
            var result = await svc.GetDashboardSummaryAsync(new AnalyticsRequest());

            Assert.Equal(18m, result.TotalHours);       // 8+6+4
            Assert.Equal(2, result.TotalUsers);          // user 1 and 2
            Assert.Equal(2, result.TotalProjects);       // project 1 and 2
        }

        [Fact]
        public async Task GetDashboardSummary_FilterByUser_OnlyCountsThatUser()
        {
            var timesheets = new List<Timesheet>
            {
                MakeTs(1, 1, 1, 8),
                MakeTs(2, 2, 2, 6)
            };
            _tsRepo.Setup(r => r.GetAllAsync()).ReturnsAsync(timesheets);

            var svc = CreateService();
            var result = await svc.GetDashboardSummaryAsync(new AnalyticsRequest { UserId = 1 });

            Assert.Equal(8m, result.TotalHours);
            Assert.Equal(1, result.TotalUsers);
        }
    }
}

    public class AnalyticsServiceAdditionalTests
    {
        private readonly Mock<IRepository<int, Timesheet>> _tsRepo = new();
        private readonly Mock<ILogger<AnalyticsService>>   _logger = new();

        private AnalyticsService CreateService() =>
            new(_tsRepo.Object, _logger.Object);

        private Timesheet MakeTs(int id, int userId, int projectId, double hours,
            TimesheetStatus status = TimesheetStatus.Approved) =>
            new Timesheet
            {
                Id = id, UserId = userId, ProjectId = projectId,
                ProjectName = $"Project {projectId}",
                WorkDate = DateTime.Today, TotalHours = hours,
                Status = status,
                StartTime = TimeSpan.FromHours(9),
                EndTime   = TimeSpan.FromHours(9 + hours),
                User = new User
                {
                    Id = userId, Name = $"User {userId}", Email = $"u{userId}@t.com",
                    EmployeeId = $"E{userId:000}", Role = UserRole.Employee,
                    PasswordHash = "h", IsActive = true
                }
            };

        // ── GetTimesheetAnalytics: empty data returns empty list ──────────────

        [Fact]
        public async Task GetAnalytics_EmptyData_ReturnsEmptyList()
        {
            _tsRepo.Setup(r => r.GetAllAsync()).ReturnsAsync(new List<Timesheet>());

            var result = await CreateService().GetTimesheetAnalyticsAsync(new AnalyticsRequest());

            Assert.Empty(result);
        }

        // ── GetTimesheetAnalytics: rejected excluded ───────────────────────────

        [Fact]
        public async Task GetAnalytics_RejectedTimesheets_Excluded()
        {
            var timesheets = new List<Timesheet>
            {
                MakeTs(1, 1, 1, 8, TimesheetStatus.Approved),
                MakeTs(2, 2, 2, 6, TimesheetStatus.Rejected)
            };
            _tsRepo.Setup(r => r.GetAllAsync()).ReturnsAsync(timesheets);

            var result = (await CreateService().GetTimesheetAnalyticsAsync(new AnalyticsRequest())).ToList();

            Assert.Single(result);
            Assert.Equal(1, result[0].UserId);
        }

        // ── GetTimesheetAnalytics: pending included ────────────────────────────

        [Fact]
        public async Task GetAnalytics_PendingTimesheets_Included()
        {
            var timesheets = new List<Timesheet>
            {
                MakeTs(1, 1, 1, 8, TimesheetStatus.Pending)
            };
            _tsRepo.Setup(r => r.GetAllAsync()).ReturnsAsync(timesheets);

            var result = (await CreateService().GetTimesheetAnalyticsAsync(new AnalyticsRequest())).ToList();

            Assert.Single(result);
        }

        // ── GetTimesheetAnalytics: filter by date range ───────────────────────

        [Fact]
        public async Task GetAnalytics_FilterByDateRange_ExcludesOutsideRange()
        {
            var ts1 = MakeTs(1, 1, 1, 8);
            ts1.WorkDate = DateTime.Today.AddDays(-10);
            var ts2 = MakeTs(2, 1, 1, 6);
            ts2.WorkDate = DateTime.Today;
            _tsRepo.Setup(r => r.GetAllAsync()).ReturnsAsync(new List<Timesheet> { ts1, ts2 });

            var result = (await CreateService().GetTimesheetAnalyticsAsync(new AnalyticsRequest
            {
                StartDate = DateTime.Today.AddDays(-5),
                EndDate   = DateTime.Today
            })).ToList();

            Assert.Single(result);
        }

        // ── GetDashboardSummary: single user single project ───────────────────

        [Fact]
        public async Task GetDashboardSummary_SingleUserSingleProject_CorrectCounts()
        {
            _tsRepo.Setup(r => r.GetAllAsync()).ReturnsAsync(
                new List<Timesheet> { MakeTs(1, 1, 1, 8) });

            var result = await CreateService().GetDashboardSummaryAsync(new AnalyticsRequest());

            Assert.Equal(8m, result.TotalHours);
            Assert.Equal(1, result.TotalUsers);
            Assert.Equal(1, result.TotalProjects);
        }

        // ── GetDashboardSummary: multiple users same project ──────────────────

        [Fact]
        public async Task GetDashboardSummary_MultipleUsersSameProject_CountsDistinctUsers()
        {
            var timesheets = new List<Timesheet>
            {
                MakeTs(1, 1, 1, 8),
                MakeTs(2, 2, 1, 6),
                MakeTs(3, 3, 1, 4)
            };
            _tsRepo.Setup(r => r.GetAllAsync()).ReturnsAsync(timesheets);

            var result = await CreateService().GetDashboardSummaryAsync(new AnalyticsRequest());

            Assert.Equal(18m, result.TotalHours);
            Assert.Equal(3, result.TotalUsers);
            Assert.Equal(1, result.TotalProjects);
        }

        // ── GetDashboardSummary: filter by project ────────────────────────────

        [Fact]
        public async Task GetDashboardSummary_FilterByProject_OnlyCountsThatProject()
        {
            var timesheets = new List<Timesheet>
            {
                MakeTs(1, 1, 1, 8),
                MakeTs(2, 1, 2, 6)
            };
            _tsRepo.Setup(r => r.GetAllAsync()).ReturnsAsync(timesheets);

            var result = await CreateService().GetDashboardSummaryAsync(
                new AnalyticsRequest { ProjectId = 1 });

            Assert.Equal(8m, result.TotalHours);
            Assert.Equal(1, result.TotalProjects);
        }

        // ── GetTimesheetAnalytics: average hours per day ──────────────────────

        [Fact]
        public async Task GetAnalytics_AverageHoursPerDay_CalculatedCorrectly()
        {
            var ts1 = MakeTs(1, 1, 1, 10);
            var ts2 = MakeTs(2, 1, 1, 6);
            ts2.WorkDate = DateTime.Today.AddDays(-1);
            _tsRepo.Setup(r => r.GetAllAsync()).ReturnsAsync(new List<Timesheet> { ts1, ts2 });

            var result = (await CreateService().GetTimesheetAnalyticsAsync(
                new AnalyticsRequest { UserId = 1, ProjectId = 1 })).ToList();

            Assert.Single(result);
            Assert.Equal(16m, result[0].TotalHoursWorked);
            Assert.Equal(2, result[0].DaysWorked);
            Assert.Equal(8m, result[0].AverageHoursPerDay);
        }

        // ── GetTimesheetAnalytics: multiple projects for same user ────────────

        [Fact]
        public async Task GetAnalytics_SameUserMultipleProjects_ReturnsMultipleGroups()
        {
            var timesheets = new List<Timesheet>
            {
                MakeTs(1, 1, 1, 8),
                MakeTs(2, 1, 2, 6)
            };
            _tsRepo.Setup(r => r.GetAllAsync()).ReturnsAsync(timesheets);

            var result = (await CreateService().GetTimesheetAnalyticsAsync(
                new AnalyticsRequest { UserId = 1 })).ToList();

            Assert.Equal(2, result.Count);
        }
    }
