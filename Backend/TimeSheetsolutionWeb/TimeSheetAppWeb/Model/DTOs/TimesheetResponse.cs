namespace TimeSheetAppWeb.Model.DTOs
{
    public class TimesheetResponse
    {
        public int Id { get; set; }
        public int UserId { get; set; }
        public string EmployeeName { get; set; } = string.Empty;
        public string EmployeeId { get; set; } = string.Empty;
        public string ProjectName { get; set; } = string.Empty;
        public DateTime Date { get; set; }

        // New properties
        public TimeSpan StartTime { get; set; }
        public TimeSpan EndTime { get; set; }
        public TimeSpan BreakTime { get; set; }

        public string HoursWorked { get; set; }
        public string? Description { get; set; }
        public TimesheetStatus Status { get; set; }
        public string? ManagerComment { get; set; }
    }
}