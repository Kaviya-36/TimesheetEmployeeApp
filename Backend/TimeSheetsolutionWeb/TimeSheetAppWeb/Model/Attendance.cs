using System.ComponentModel.DataAnnotations;

namespace TimeSheetAppWeb.Model
{
    public class Attendance
    {
        [Key]
        public int Id { get; set; }

        [Required]
        public int UserId { get; set; }

        public User? User { get; set; }

        [Required]
        public DateTime Date { get; set; }

        public TimeSpan? CheckIn { get; set; }

        public TimeSpan? CheckOut { get; set; }

        public bool IsLate { get; set; } = false;

        public TimeSpan TotalHours { get; set; } = TimeSpan.Zero;
    }
}