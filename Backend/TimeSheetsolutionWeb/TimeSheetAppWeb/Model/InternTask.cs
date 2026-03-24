namespace TimeSheetAppWeb.Model
{
    public enum TaskStatus
    {
        Pending = 1,
        InProgress = 2,
        Completed = 3
    }
    public class InternTask : IComparable<InternTask>, IEquatable<InternTask>
    {
        public int Id { get; set; }

        public int InternId { get; set; }
        public User? Intern { get; set; }

        public string? Title { get; set; }
        public string? Description { get; set; }

        public DateTime AssignedDate { get; set; }
        public DateTime? DueDate { get; set; }

        public TaskStatus Status { get; set; } = TaskStatus.Pending;

        public int CompareTo(InternTask? other)
        {
            if (other == null) return 1;

            int dueDateComparison = Nullable.Compare(this.DueDate, other.DueDate);
            if (dueDateComparison != 0)
                return dueDateComparison;

            return this.AssignedDate.CompareTo(other.AssignedDate);
        }
        public bool Equals(InternTask? other)
        {
            if (other == null) return false;

            return this.Id == other.Id;
        }

    }
}
