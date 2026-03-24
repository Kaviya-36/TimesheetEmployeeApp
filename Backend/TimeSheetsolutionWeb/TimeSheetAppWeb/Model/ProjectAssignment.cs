using TimeSheetAppWeb.Model;

public class ProjectAssignment : IComparable<ProjectAssignment>, IEquatable<ProjectAssignment>
{
    public int Id { get; set; }

    public int UserId { get; set; }
    public User? User { get; set; }

    public int ProjectId { get; set; }
    public Project? Project { get; set; }

    public DateTime AssignedDate { get; set; } = DateTime.Now;
    public int CompareTo(ProjectAssignment? other)
    {
        if (other == null) return 1;

        int dateComparison = this.AssignedDate.CompareTo(other.AssignedDate);
        if (dateComparison != 0)
            return dateComparison;

        int userComparison = this.UserId.CompareTo(other.UserId);
        if (userComparison != 0)
            return userComparison;

        return this.ProjectId.CompareTo(other.ProjectId);
    }

    public bool Equals(ProjectAssignment? other)
    {
        if (other == null) return false;

        return this.Id == other.Id;
    }
}
