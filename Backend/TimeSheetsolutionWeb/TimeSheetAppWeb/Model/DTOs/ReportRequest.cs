namespace TimeSheetAppWeb.Model.DTOs
{
    // Request for report generation
    public class ReportRequest
    {
        public int? ProjectId { get; set; }
        public int? UserId { get; set; }
        public DateTime? StartDate { get; set; }
        public DateTime? EndDate { get; set; }
        public int PageNumber { get; set; } = 1;
        public int PageSize { get; set; } = 10;
    }

    // Individual timesheet report item
    public class TimesheetReportItem
    {
        public int TimesheetId { get; set; }
        public string ProjectName { get; set; }
        public string UserName { get; set; }
        public DateTime Date { get; set; }
        public decimal HoursWorked { get; set; }
        public string Description { get; set; }
    }

    // Paged report response
    public class PagedReportResponse<T>
    {
        public IEnumerable<T> Data { get; set; }
        public int TotalItems { get; set; }
        public int TotalPages { get; set; }
        public int CurrentPage { get; set; }
        public int PageSize { get; set; }
    }

    // Analytics per user/project
    public class TimesheetAnalyticsItem
    {
        public int ProjectId { get; set; }
        public string ProjectName { get; set; }
        public int UserId { get; set; }
        public string UserName { get; set; }
        public decimal TotalHoursWorked { get; set; }
        public int DaysWorked { get; set; }
        public decimal AverageHoursPerDay { get; set; }
    }

    // Dashboard summary
    public class TimesheetDashboardSummary
    {
        public decimal TotalHours { get; set; }
        public decimal AverageHoursPerUser { get; set; }
        public int TotalUsers { get; set; }
        public int TotalProjects { get; set; }
        public int ActiveProjects { get; set; }
    }

    // Analytics request
    public class AnalyticsRequest
    {
        public int? ProjectId { get; set; }
        public int? UserId { get; set; }
        public DateTime? StartDate { get; set; }
        public DateTime? EndDate { get; set; }
    }
}