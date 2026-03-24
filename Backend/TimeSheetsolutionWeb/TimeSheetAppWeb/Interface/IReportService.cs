using TimeSheetAppWeb.Model.DTOs;

namespace TimeSheetAppWeb.Interface
{
    public interface IReportService
    {
        Task<PagedReportResponse<TimesheetReportItem>> GetTimesheetReportAsync(ReportRequest request);
    }

    public interface IAnalyticsService
    {
        Task<IEnumerable<TimesheetAnalyticsItem>> GetTimesheetAnalyticsAsync(AnalyticsRequest request);
        Task<TimesheetDashboardSummary> GetDashboardSummaryAsync(AnalyticsRequest request);
    }
}