using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace TimeSheetAppWeb.Model
{
    public enum TimesheetStatus
    {
        Pending,
        Approved,
        Rejected
    }

    [Index(nameof(UserId), nameof(ProjectId), nameof(WorkDate), IsUnique = true)]
    public class Timesheet
    {
        [Key]
        public int Id { get; set; }

        [Required]
        public int UserId { get; set; }

        public User? User { get; set; }

        [Required]
        public int ProjectId { get; set; }
        public string? ProjectName { get; set; }

        public Project? Project { get; set; }

        [Required]
        public DateTime WorkDate { get; set; }

        [Required]
        public TimeSpan StartTime { get; set; }

        [Required]
        public TimeSpan EndTime { get; set; }

        public TimeSpan BreakTime { get; set; } = TimeSpan.Zero;

        [Range(0, 24, ErrorMessage = "Total hours must be between 0 and 24")]
        public double TotalHours { get; set; }

        [StringLength(1000)]
        public string? TaskDescription { get; set; }

        [Required]
        public TimesheetStatus Status { get; set; } = TimesheetStatus.Pending;

        public int? ApprovedById { get; set; }

        [ForeignKey("ApprovedById")]
        public User? ApprovedBy { get; set; }

        [StringLength(500)]
        public string? ManagerComment { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    }
}