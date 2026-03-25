using Microsoft.EntityFrameworkCore;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace TimeSheetAppWeb.Model
{
    public class InternDetails
    {
        [Key]
        public int Id { get; set; }

        [Required]
        public int UserId { get; set; }

        public User? User { get; set; }

        [Required(ErrorMessage = "Training start date is required")]
        public DateTime TrainingStart { get; set; }

        [Required(ErrorMessage = "Training end date is required")]
        public DateTime TrainingEnd { get; set; }

        public int? MentorId { get; set; }

        [ForeignKey("MentorId")]
        public User? Mentor { get; set; }
    }
}