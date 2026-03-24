namespace TimeSheetAppWeb.Model
{
    public class Attendance : IComparable<Attendance>, IEquatable<Attendance>
    {
        public int Id { get; set; }

        public int UserId { get; set; }
        public User? User { get; set; }

        public DateTime Date { get; set; }
        public TimeSpan? CheckIn { get; set; }
        public TimeSpan? CheckOut { get; set; }
        public bool IsLate { get; set; }
        public TimeSpan TotalHours { get; set; }

        public int CompareTo(Attendance? other)
        {
            if (other == null) return 1;

            int dateComparison = this.Date.CompareTo(other.Date);
            if (dateComparison != 0)
                return dateComparison;

            return this.UserId.CompareTo(other.UserId);
        }

        public bool Equals(Attendance? other)
        {
            if (other == null) return false;

            return this.Id == other.Id &&
                   this.UserId == other.UserId &&
                   this.Date.Date == other.Date.Date;
        }
    }
}