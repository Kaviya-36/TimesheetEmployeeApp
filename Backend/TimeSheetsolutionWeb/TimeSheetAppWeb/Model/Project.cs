namespace TimeSheetAppWeb.Model
{
    public enum ProjectStatus
    {
        Active,
        Completed,
        OnHold
    }

    public class Project : IComparable<Project>, IEquatable<Project>
    {
        public int Id { get; set; }
        public string ProjectName { get; set; } = string.Empty;
        public string? Description { get; set; }

        public int? ManagerId { get; set; } // <-- nullable now
        public User? Manager { get; set; }

        public DateTime StartDate { get; set; }
        public DateTime? EndDate { get; set; }

        public ICollection<ProjectAssignment>? ProjectAssignments { get; set; }
        public ICollection<Timesheet>? Timesheets { get; set; }

        public int CompareTo(Project? other)
        {
            if (other == null) return 1;

            int startDateComparison = this.StartDate.CompareTo(other.StartDate);
            if (startDateComparison != 0)
                return startDateComparison;

            return string.Compare(this.ProjectName, other.ProjectName, StringComparison.OrdinalIgnoreCase);
        }

        public bool Equals(Project? other)
        {
            if (other == null) return false;

            return this.Id == other.Id;
        }
    }
}
