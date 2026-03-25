using System.ComponentModel.DataAnnotations;
using Microsoft.EntityFrameworkCore;

namespace TimeSheetAppWeb.Model
{
    public enum UserRole
    {
        Admin,
        Manager,
        HR,
        Employee,
        Mentor,
        Intern
    }

    [Index(nameof(EmployeeId), IsUnique = true)]
    [Index(nameof(Email), IsUnique = true)]
    public class User
    {
        [Key]
        public int Id { get; set; }

        [Required(ErrorMessage = "Employee ID is required")]
        [StringLength(20)]
        public string EmployeeId { get; set; } = string.Empty;

        [Required(ErrorMessage = "Name is required")]
        [StringLength(100, MinimumLength = 2)]
        public string Name { get; set; } = string.Empty;

        [Required(ErrorMessage = "Email is required")]
        [EmailAddress]
        [StringLength(150)]
        public string Email { get; set; } = string.Empty;

        [Required]
        public string PasswordHash { get; set; } = string.Empty;

        [Phone]
        [StringLength(15)]
        public string? Phone { get; set; }

        public int? DepartmentId { get; set; }
        public Department? Department { get; set; }

        [Required]
        public UserRole Role { get; set; } = UserRole.Employee;

        public bool IsActive { get; set; } = true;

        [Required]
        public DateTime JoiningDate { get; set; } = DateTime.UtcNow;

        public ICollection<Timesheet> Timesheets { get; set; } = new List<Timesheet>();
        public ICollection<LeaveRequest> LeaveRequests { get; set; } = new List<LeaveRequest>();
        public ICollection<Attendance> Attendances { get; set; } = new List<Attendance>();
        public ICollection<ProjectAssignment> ProjectAssignments { get; set; } = new List<ProjectAssignment>();
    }
}