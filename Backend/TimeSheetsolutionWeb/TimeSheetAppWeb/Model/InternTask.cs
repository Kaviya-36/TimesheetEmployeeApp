using System.ComponentModel.DataAnnotations;
using Microsoft.EntityFrameworkCore;

namespace TimeSheetAppWeb.Model
{
    public enum TaskStatus
    {
        Pending = 1,
        InProgress = 2,
        Completed = 3
    }

    public class InternTask
    {
        [Key]
        public int Id { get; set; }

        [Required]
        public int InternId { get; set; }

        public User? Intern { get; set; }

        [Required(ErrorMessage = "Title is required")]
        public string Title { get; set; } = string.Empty;
        public string? Description { get; set; }

        [Required]
        public DateTime AssignedDate { get; set; }

        public DateTime? DueDate { get; set; }

        [Required]
        public TaskStatus Status { get; set; } = TaskStatus.Pending;
    }
}