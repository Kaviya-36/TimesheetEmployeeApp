namespace TimeSheetAppWeb.Model.DTOs
{
    public class TimesheetGridRequest
    {
        public int ProjectId { get; set; }
        public string ProjectName { get; set; } = string.Empty;
        public string WorkDate { get; set; } = string.Empty;  // "2026-03-28"
        public double Hours { get; set; }
        public string? TaskDescription { get; set; }
    }
}
