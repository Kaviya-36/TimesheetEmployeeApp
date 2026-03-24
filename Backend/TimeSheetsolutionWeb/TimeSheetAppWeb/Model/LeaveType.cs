namespace TimeSheetAppWeb.Model
{
    public class LeaveType : IComparable<LeaveType>, IEquatable<LeaveType>
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public int MaxDaysPerYear { get; set; }
        public bool IsActive { get; set; }

        public int CompareTo(LeaveType? other)
        {
            if (other == null) return 1;

            int nameComparison = string.Compare(this.Name, other.Name, StringComparison.OrdinalIgnoreCase);
            if (nameComparison != 0)
                return nameComparison;

            return this.MaxDaysPerYear.CompareTo(other.MaxDaysPerYear);
        }

        public bool Equals(LeaveType? other)
        {
            if (other == null) return false;

            return this.Id == other.Id;
        }
    }
}