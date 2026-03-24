namespace TimeSheetAppWeb.Model
{
    public enum TimesheetStatus
    {
        Pending,
        Approved,
        Rejected
    }

    public class Timesheet : IComparable<Timesheet>, IEquatable<Timesheet>
    {
        public int Id { get; set; }

        public int UserId { get; set; }
        public User? User { get; set; }

        public int ProjectId { get; set; }
        public string ProjectName { get; set; }
        public Project? Project { get; set; }

        public DateTime WorkDate { get; set; }

        public TimeSpan StartTime { get; set; }
        public TimeSpan EndTime { get; set; }
        public TimeSpan BreakTime { get; set; }
        public double TotalHours { get; set; }

        public string? TaskDescription { get; set; }

        public TimesheetStatus Status { get; set; } = TimesheetStatus.Pending;

        public int? ApprovedById { get; set; }
        public User? ApprovedBy { get; set; }

        public string? ManagerComment { get; set; }
        public DateTime CreatedAt { get; set; } = DateTime.Now;
        public int CompareTo(Timesheet? other)
        {
            if (other == null) return 1;

            int dateComparison = this.WorkDate.CompareTo(other.WorkDate);
            if (dateComparison != 0)
                return dateComparison;

            int startTimeComparison = this.StartTime.CompareTo(other.StartTime);
            if (startTimeComparison != 0)
                return startTimeComparison;

            return this.UserId.CompareTo(other.UserId);
        }

        public bool Equals(Timesheet? other)
        {
            if (other == null) return false;

            return this.Id == other.Id;
        }

    }
}
