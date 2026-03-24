namespace TimeSheetAppWeb.Model.DTOs
{
    public class TimesheetCreateRequest
    {
        public int ProjectId { get; set; }
        public string ProjectName { get; set; }
        public DateTime WorkDate { get; set; }
        public TimeSpan StartTime { get; set; }
        public TimeSpan EndTime { get; set; }
        public TimeSpan BreakTime { get; set; }
        public string? TaskDescription { get; set; }

    }
}
