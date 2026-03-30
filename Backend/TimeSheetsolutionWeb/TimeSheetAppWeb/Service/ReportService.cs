using Microsoft.Extensions.Logging;
using TimeSheetAppWeb.Interface;
using TimeSheetAppWeb.Model;
using TimeSheetAppWeb.Model.DTOs;

namespace TimeSheetAppWeb.Services
{
    public class ReportService : IReportService
    {
        private readonly IRepository<int, Timesheet> _timesheetRepository;
        private readonly ILogger<ReportService> _logger;

        public ReportService(IRepository<int, Timesheet> timesheetRepository,
                             ILogger<ReportService> logger)
        {
            _timesheetRepository = timesheetRepository;
            _logger = logger;
        }

        public async Task<PagedReportResponse<TimesheetReportItem>> GetTimesheetReportAsync(ReportRequest request)
        {
            try
            {
                _logger.LogInformation("Fetching Timesheet Report");

                var timesheets = await _timesheetRepository.GetAllAsync();

                if (timesheets == null)
                {
                    _logger.LogWarning("No timesheet records found.");
                    return new PagedReportResponse<TimesheetReportItem>();
                }

                var query = timesheets.AsQueryable();

                // Filter by project
                if (request.ProjectId.HasValue)
                    query = query.Where(t => t.ProjectId == request.ProjectId.Value);

                // Filter by user
                if (request.UserId.HasValue)
                    query = query.Where(t => t.UserId == request.UserId.Value);

                // Filter by date range
                if (request.StartDate.HasValue)
                    query = query.Where(t => t.WorkDate >= request.StartDate.Value);

                if (request.EndDate.HasValue)
                    query = query.Where(t => t.WorkDate <= request.EndDate.Value);

                // Filter by status
                query = query.Where(t =>
                    t.Status == TimesheetStatus.Approved ||
                    t.Status == TimesheetStatus.Pending);

                var totalItems = query.Count();

                var data = query
                    .OrderByDescending(t => t.WorkDate)
                    .Skip((request.PageNumber - 1) * request.PageSize)
                    .Take(request.PageSize)
                    .Select(t => new TimesheetReportItem
                    {
                        TimesheetId = t.Id,
                        ProjectName = t.ProjectName,
                        UserName = t.User != null ? t.User.Name : "",
                        Date = t.WorkDate,
                        HoursWorked = (decimal)t.TotalHours,
                        Description = t.TaskDescription
                    })
                    .ToList();

                _logger.LogInformation("Timesheet Report generated successfully.");

                return new PagedReportResponse<TimesheetReportItem>
                {
                    Data = data,
                    TotalItems = totalItems,
                    TotalPages = (int)Math.Ceiling(totalItems / (double)request.PageSize),
                    CurrentPage = request.PageNumber,
                    PageSize = request.PageSize
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error while generating Timesheet Report");
                throw;
            }
        }
    }
}