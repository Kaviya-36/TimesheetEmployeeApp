namespace TimeSheetAppWeb.Model
{
    public enum LeaveStatus
    {
        Pending,
        Approved,
        Rejected
    }

    public class LeaveRequest : IComparable<LeaveRequest>, IEquatable<LeaveRequest>
    {
        public int Id { get; set; }

        public int UserId { get; set; }
        public User? User { get; set; }

        public int LeaveTypeId { get; set; }
        public LeaveType? LeaveType { get; set; }

        public DateTime FromDate { get; set; }
        public DateTime ToDate { get; set; }
        public string? Reason { get; set; }

        public LeaveStatus Status { get; set; } = LeaveStatus.Pending;

        public int? ApprovedById { get; set; }
        public User? ApprovedBy { get; set; }

        public string? ManagerComment { get; set; }
        public DateTime AppliedDate { get; set; } = DateTime.Now;
        public DateTime? ApprovedDate { get; set; }
        public int CompareTo(LeaveRequest? other)
        {
            if (other == null) return 1;

            int fromComparison = this.FromDate.CompareTo(other.FromDate);
            if (fromComparison != 0) return fromComparison;

            int toComparison = this.ToDate.CompareTo(other.ToDate);
            if (toComparison != 0) return toComparison;

            return this.AppliedDate.CompareTo(other.AppliedDate);
        }

        public bool Equals(LeaveRequest? other)
        {
            if (other == null) return false;
            return this.Id == other.Id;
        }
    }
}