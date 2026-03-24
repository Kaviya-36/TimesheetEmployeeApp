namespace TimeSheetAppWeb.Model
{
    public class InternDetails : IComparable<InternDetails>, IEquatable<InternDetails>
    {
        public int Id { get; set; }

        public int UserId { get; set; }
        public User? User { get; set; }

        public DateTime TrainingStart { get; set; }
        public DateTime TrainingEnd { get; set; }

        public int? MentorId { get; set; }
        public User? Mentor { get; set; }

        public int CompareTo(InternDetails? other)
        {
            if (other == null) return 1;

            int startComparison = this.TrainingStart.CompareTo(other.TrainingStart);
            if (startComparison != 0)
                return startComparison;

            return this.TrainingEnd.CompareTo(other.TrainingEnd);
        }

        public bool Equals(InternDetails? other)
        {
            if (other == null) return false;

            return this.UserId == other.UserId;
        }
    }
}
    
