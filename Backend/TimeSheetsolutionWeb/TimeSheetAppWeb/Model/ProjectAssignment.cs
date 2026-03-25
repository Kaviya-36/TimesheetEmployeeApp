using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;
using TimeSheetAppWeb.Model;

public class ProjectAssignment
{
    [Key]
    public int Id { get; set; }

    [Required]
    public int UserId { get; set; }

    public User? User { get; set; }

    [Required]
    public int ProjectId { get; set; }

    public Project? Project { get; set; }

    [Required]
    public DateTime AssignedDate { get; set; } = DateTime.UtcNow;
}