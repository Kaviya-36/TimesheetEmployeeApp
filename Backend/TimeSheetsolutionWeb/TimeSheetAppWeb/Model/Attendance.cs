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

        [Range(typeof(TimeSpan), "00:00:00", "23:59:59",
            ErrorMessage = "Total hours must be between 0 and 24 hours")]
        public TimeSpan TotalHours { get; set; } = TimeSpan.Zero;
    }
}