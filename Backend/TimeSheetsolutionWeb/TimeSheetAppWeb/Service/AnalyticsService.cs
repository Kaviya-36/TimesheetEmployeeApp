using Microsoft.Extensions.Logging;
using TimeSheetAppWeb.Interface;
using TimeSheetAppWeb.Model;
using TimeSheetAppWeb.Model.DTOs;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace TimeSheetAppWeb.Services
{
    public class AnalyticsService : IAnalyticsService
    {
        private readonly IRepository<int, Timesheet> _timesheetRepository;
        private readonly ILogger<AnalyticsService> _logger;

        public AnalyticsService(
            IRepository<int, Timesheet> timesheetRepository,
            ILogger<AnalyticsService> logger)
        {
            _timesheetRepository = timesheetRepository;
            _logger = logger;
        }

        // Analytics per user/project
        public async Task<IEnumerable<TimesheetAnalyticsItem>> GetTimesheetAnalyticsAsync(AnalyticsRequest request)
        {
            try
            {
                // Get all timesheets from repository
                var timesheets = (await _timesheetRepository.GetAllAsync()) ?? new List<Timesheet>();

                // Apply filtering in-memory
                var filtered = timesheets.Where(t =>
                    (!request.ProjectId.HasValue || t.ProjectId == request.ProjectId.Value) &&
                    (!request.UserId.HasValue || t.UserId == request.UserId.Value) &&
                    (!request.StartDate.HasValue || t.WorkDate >= request.StartDate.Value) &&
                    (!request.EndDate.HasValue || t.WorkDate <= request.EndDate.Value) &&
                    (t.Status == TimesheetStatus.Approved || t.Status == TimesheetStatus.Pending)
                ).ToList();

                // Group and calculate analytics
                var analytics = filtered
                    .GroupBy(t => new { t.ProjectId, t.ProjectName, t.UserId, t.User.Name })
                    .Select(g => new TimesheetAnalyticsItem
                    {
                        ProjectId = g.Key.ProjectId,
                        ProjectName = g.Key.ProjectName,
                        UserId = g.Key.UserId,
                        UserName = g.Key.Name,
                        TotalHoursWorked = (decimal)g.Sum(t => t.TotalHours),
                        DaysWorked = g.Select(t => t.WorkDate.Date).Distinct().Count(),
                        AverageHoursPerDay = (decimal)g.Sum(t => t.TotalHours) / g.Select(t => t.WorkDate.Date).Distinct().Count()
                    })
                    .ToList();

                _logger.LogInformation("Timesheet analytics calculated for ProjectId={ProjectId}, UserId={UserId}", request.ProjectId, request.UserId);

                return analytics;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error fetching timesheet analytics");
                throw;
            }
        }

        // Dashboard summary
        public async Task<TimesheetDashboardSummary> GetDashboardSummaryAsync(AnalyticsRequest request)
        {
            try
            {
                var timesheets = (await _timesheetRepository.GetAllAsync()) ?? new List<Timesheet>();

                var filtered = timesheets.Where(t =>
                    (!request.ProjectId.HasValue || t.ProjectId == request.ProjectId.Value) &&
                    (!request.UserId.HasValue || t.UserId == request.UserId.Value) &&
                    (!request.StartDate.HasValue || t.WorkDate >= request.StartDate.Value) &&
                    (!request.EndDate.HasValue || t.WorkDate <= request.EndDate.Value) &&
                    (t.Status == TimesheetStatus.Approved || t.Status == TimesheetStatus.Pending)
                ).ToList();

                var totalHours = filtered.Sum(t => t.TotalHours);
                var totalUsers = filtered.Select(t => t.UserId).Distinct().Count();
                var totalProjects = filtered.Select(t => t.ProjectId).Distinct().Count();
                var activeProjects = filtered.Where(t => t.TotalHours > 0).Select(t => t.ProjectId).Distinct().Count();
                var averageHoursPerUser = totalUsers > 0 ? totalHours / totalUsers : 0;

                _logger.LogInformation("Dashboard summary calculated for ProjectId={ProjectId}, UserId={UserId}", request.ProjectId, request.UserId);

                return new TimesheetDashboardSummary
                {
                    TotalHours = (decimal)totalHours,
                    AverageHoursPerUser = (decimal)averageHoursPerUser,
                    TotalUsers = totalUsers,
                    TotalProjects = totalProjects,
                    ActiveProjects = activeProjects
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error calculating dashboard summary");
                throw;
            }
        }
    }
}