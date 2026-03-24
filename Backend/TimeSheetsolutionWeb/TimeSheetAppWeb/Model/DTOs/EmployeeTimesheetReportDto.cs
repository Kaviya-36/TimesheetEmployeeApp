using TimeSheetAppWeb.Model;

public class EmployeeTimesheetReportDto
{
    public string? EmployeeName { get; set; }
    public string? ProjectName { get; set; }
    public DateTime WorkDate { get; set; }
    public double TotalHours { get; set; }
    public TimesheetStatus Status { get; set; }
}