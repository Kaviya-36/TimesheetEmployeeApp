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
