namespace TimeSheetAppWeb.Model.DTOs
{
    public class TimesheetWeeklyEntry
    {
        public int ProjectId { get; set; }
        public string ProjectName { get; set; } = string.Empty;
        public string WorkDate { get; set; } = string.Empty;   // "2026-03-28"
        public double Hours { get; set; }
        public string? TaskDescription { get; set; }
    }

    public class TimesheetWeeklyRequest
    {
        public List<TimesheetWeeklyEntry> Entries { get; set; } = new();
        public bool Submit { get; set; } = false;   // false = save draft, true = submit for approval
    }

    public class TimesheetWeeklyResponse
    {
        public int Saved { get; set; }
        public int Updated { get; set; }
        public int Skipped { get; set; }
        public int AlreadyApproved { get; set; }
        public List<string> Errors { get; set; } = new();
    }
}
