namespace TimeSheetAppWeb.Model
{
    public class Department : IComparable<Department>, IEquatable<Department>
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;

        public ICollection<User>? Users { get; set; }
        public int CompareTo(Department? other)
        {
            if (other == null) return 1;
            return string.Compare(this.Name, other.Name, StringComparison.OrdinalIgnoreCase);
        }

        public bool Equals(Department? other)
        {
            if (other == null) return false;

            return this.Id == other.Id;
        }
    }
}
