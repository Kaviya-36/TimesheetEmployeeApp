using System.ComponentModel.DataAnnotations;
using Microsoft.EntityFrameworkCore;

namespace TimeSheetAppWeb.Model
{
    [Index(nameof(Name), IsUnique = true)]
    public class LeaveType
    {
        [Key]
        public int Id { get; set; }

        [Required(ErrorMessage = "Leave type name is required")]
        [StringLength(100, MinimumLength = 2,
            ErrorMessage = "Name must be between 2 and 100 characters")]
        public string Name { get; set; } = string.Empty;

        [Range(0, 365, ErrorMessage = "Max days must be between 0 and 365")]
        public int MaxDaysPerYear { get; set; }

        public bool IsActive { get; set; } = true;
    }
}