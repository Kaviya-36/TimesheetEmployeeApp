using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace TimeSheetAppWeb.Model
{
    public enum LeaveStatus
    {
        Pending,
        Approved,
        Rejected
    }

    public class LeaveRequest
    {
        [Key]
        public int Id { get; set; }

        [Required]
        public int UserId { get; set; }

        public User? User { get; set; }

        [Required]
        public int LeaveTypeId { get; set; }

        public LeaveType? LeaveType { get; set; }

        [Required(ErrorMessage = "From date is required")]
        public DateTime FromDate { get; set; }

        [Required(ErrorMessage = "To date is required")]
        public DateTime ToDate { get; set; }

        [Required(ErrorMessage = "Reason is required")]
        public string Reason { get; set; } = string.Empty;

        [Required]
        public LeaveStatus Status { get; set; } = LeaveStatus.Pending;

        public int? ApprovedById { get; set; }

        [ForeignKey("ApprovedById")]
        public User? ApprovedBy { get; set; }

        [StringLength(500)]
        public string? ManagerComment { get; set; }

        public DateTime AppliedDate { get; set; } = DateTime.UtcNow;

        public DateTime? ApprovedDate { get; set; }
    }
}