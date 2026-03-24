namespace TimeSheetAppWeb.Model.DTOs
{
    public class TimesheetUpdateRequest
    {
        public DateTime? WorkDate { get; set; }
        public TimeSpan? StartTime { get; set; }
        public TimeSpan? EndTime { get; set; }
        public TimeSpan? BreakTime { get; set; }
        public string? TaskDescription { get; set; }
        public TimesheetStatus? Status { get; set; } 
    }
}
