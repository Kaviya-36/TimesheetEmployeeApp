using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace TimeSheetAppWeb.Model
{
    public enum ProjectStatus
    {
        Active,
        Completed,
        OnHold
    }

    [Index(nameof(ProjectName), IsUnique = true)]
    public class Project
    {
        [Key]
        public int Id { get; set; }

        [Required(ErrorMessage = "Project name is required")]
        public string ProjectName { get; set; } = string.Empty;

        [StringLength(1000)]
        public string? Description { get; set; }

        public int? ManagerId { get; set; }

        [ForeignKey("ManagerId")]
        public User? Manager { get; set; }

        [Required(ErrorMessage = "Start date is required")]
        public DateTime StartDate { get; set; }

        public DateTime? EndDate { get; set; }

        [Required]
        public ProjectStatus Status { get; set; } = ProjectStatus.Active;

        public ICollection<ProjectAssignment> ProjectAssignments { get; set; } = new List<ProjectAssignment>();

        public ICollection<Timesheet> Timesheets { get; set; } = new List<Timesheet>();
    }
}