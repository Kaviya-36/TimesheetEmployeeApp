using System;
using System.Collections.Generic;

namespace TimeSheetAppWeb.Model
{
    public class User : IComparable<User>, IEquatable<User>
    {
        public int Id { get; set; }

        public string EmployeeId { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public string PasswordHash { get; set; } = string.Empty;
        public string Phone { get; set; } = string.Empty;

        // Foreign Key
        public int? DepartmentId { get; set; }
        public Department? Department { get; set; }

        // Store as string in DB
        public UserRole Role { get; set; } = UserRole.Employee;

        // Store as bit (0/1) in DB
        public bool IsActive { get; set; } = true;

        public DateTime JoiningDate { get; set; } = DateTime.UtcNow;

        public ICollection<Timesheet> Timesheets { get; set; } = new List<Timesheet>();
        public ICollection<LeaveRequest> LeaveRequests { get; set; } = new List<LeaveRequest>();
        public ICollection<Attendance> Attendances { get; set; } = new List<Attendance>();
        public ICollection<ProjectAssignment> ProjectAssignments { get; set; } = new List<ProjectAssignment>();

        public int CompareTo(User? other)
            => other == null ? 1 :
               string.Compare(Name, other.Name, StringComparison.OrdinalIgnoreCase);

        public bool Equals(User? other)
            => other != null && Id == other.Id;

        public override bool Equals(object? obj)
            => Equals(obj as User);

        public override int GetHashCode()
            => Id.GetHashCode();
    }
    public enum UserRole
    {
        Admin,
        Manager,
        HR,
        Employee,
        Mentor,
        Intern
    }
}