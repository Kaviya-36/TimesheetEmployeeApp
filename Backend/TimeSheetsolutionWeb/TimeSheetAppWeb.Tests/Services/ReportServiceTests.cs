using Microsoft.Extensions.Logging;
using Moq;
using TimeSheetAppWeb.Interface;
using TimeSheetAppWeb.Model;
using TimeSheetAppWeb.Model.DTOs;
using TimeSheetAppWeb.Services;
using Xunit;

namespace TimeSheetAppWeb.Tests.Services
{
    public class ReportServiceTests
    {
        private readonly Mock<IRepository<int, Timesheet>> _tsRepo = new();
        private readonly Mock<ILogger<ReportService>>      _logger = new();

        private ReportService CreateService() =>
            new(_tsRepo.Object, _logger.Object);

        private Timesheet MakeTs(int id, int userId, int projectId, double hours,
            TimesheetStatus status = TimesheetStatus.Approved,
            DateTime? date = null) =>
            new Timesheet
            {
                Id = id, UserId = userId, ProjectId = projectId,
                ProjectName = $"Project {projectId}",
                WorkDate = date ?? DateTime.Today,
                TotalHours = hours, Status = status,
                TaskDescription = $"Task {id}",
                StartTime = TimeSpan.FromHours(9),
                EndTime   = TimeSpan.FromHours(9 + hours)
            };

        // ── GetTimesheetReportAsync ────────────────────────────────────────────

        [Fact]
        public async Task GetReport_NullData_ReturnsEmptyResponse()
        {
            _tsRepo.Setup(r => r.GetAllAsync()).ReturnsAsync((IEnumerable<Timesheet>?)null);

            var svc = CreateService();
            var result = await svc.GetTimesheetReportAsync(new ReportRequest
            {
                PageNumber = 1, PageSize = 10
            });

            Assert.NotNull(result);
            Assert.True(result.Data == null || !result.Data.Any());
        }

        [Fact]
        public async Task GetReport_ExcludesRejected()
        {
            var timesheets = new List<Timesheet>
            {
                MakeTs(1, 1, 1, 8, TimesheetStatus.Approved),
                MakeTs(2, 1, 1, 4, TimesheetStatus.Pending),
                MakeTs(3, 1, 1, 6, TimesheetStatus.Rejected)
            };
            _tsRepo.Setup(r => r.GetAllAsync()).ReturnsAsync(timesheets);

            var svc = CreateService();
            var result = await svc.GetTimesheetReportAsync(new ReportRequest
            {
                PageNumber = 1, PageSize = 10
            });

            Assert.Equal(2, result.TotalItems);
            Assert.Equal(2, result.Data!.Count());
        }

        [Fact]
        public async Task GetReport_FilterByProject_ReturnsOnlyThatProject()
        {
            var timesheets = new List<Timesheet>
            {
                MakeTs(1, 1, 1, 8),
                MakeTs(2, 1, 2, 6)
            };
            _tsRepo.Setup(r => r.GetAllAsync()).ReturnsAsync(timesheets);

            var svc = CreateService();
            var result = await svc.GetTimesheetReportAsync(new ReportRequest
            {
                ProjectId = 1, PageNumber = 1, PageSize = 10
            });

            Assert.Equal(1, result.TotalItems);
            Assert.Equal("Project 1", result.Data!.First().ProjectName);
        }

        [Fact]
        public async Task GetReport_FilterByUser_ReturnsOnlyThatUser()
        {
            var timesheets = new List<Timesheet>
            {
                MakeTs(1, 1, 1, 8),
                MakeTs(2, 2, 1, 6)
            };
            _tsRepo.Setup(r => r.GetAllAsync()).ReturnsAsync(timesheets);

            var svc = CreateService();
            var result = await svc.GetTimesheetReportAsync(new ReportRequest
            {
                UserId = 2, PageNumber = 1, PageSize = 10
            });

            Assert.Equal(1, result.TotalItems);
            Assert.Equal(2, result.Data!.First().TimesheetId);
        }

        [Fact]
        public async Task GetReport_FilterByDateRange_ReturnsCorrectItems()
        {
            var timesheets = new List<Timesheet>
            {
                MakeTs(1, 1, 1, 8, date: DateTime.Today.AddDays(-10)),
                MakeTs(2, 1, 1, 6, date: DateTime.Today.AddDays(-5)),
                MakeTs(3, 1, 1, 4, date: DateTime.Today)
            };
            _tsRepo.Setup(r => r.GetAllAsync()).ReturnsAsync(timesheets);

            var svc = CreateService();
            var result = await svc.GetTimesheetReportAsync(new ReportRequest
            {
                StartDate  = DateTime.Today.AddDays(-6),
                EndDate    = DateTime.Today,
                PageNumber = 1, PageSize = 10
            });

            Assert.Equal(2, result.TotalItems); // -5 days and today
        }

        [Fact]
        public async Task GetReport_Pagination_ReturnsCorrectPage()
        {
            var timesheets = Enumerable.Range(1, 15)
                .Select(i => MakeTs(i, 1, 1, 8, date: DateTime.Today.AddDays(-i)))
                .ToList();
            _tsRepo.Setup(r => r.GetAllAsync()).ReturnsAsync(timesheets);

            var svc = CreateService();
            var result = await svc.GetTimesheetReportAsync(new ReportRequest
            {
                PageNumber = 2, PageSize = 5
            });

            Assert.Equal(15, result.TotalItems);
            Assert.Equal(3, result.TotalPages);
            Assert.Equal(5, result.Data!.Count());
            Assert.Equal(2, result.CurrentPage);
        }

        [Fact]
        public async Task GetReport_HoursWorked_MappedCorrectly()
        {
            var timesheets = new List<Timesheet> { MakeTs(1, 1, 1, 7.5) };
            _tsRepo.Setup(r => r.GetAllAsync()).ReturnsAsync(timesheets);

            var svc = CreateService();
            var result = await svc.GetTimesheetReportAsync(new ReportRequest
            {
                PageNumber = 1, PageSize = 10
            });

            Assert.Equal(7.5m, result.Data!.First().HoursWorked);
        }
    }
}
