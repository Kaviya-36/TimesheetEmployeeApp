using Microsoft.EntityFrameworkCore;
using System.ComponentModel.DataAnnotations;

namespace TimeSheetAppWeb.Model
{
    [Index(nameof(Name), IsUnique = true)]
    public class Department
    {
        [Key]
        public int Id { get; set; }

        [Required(ErrorMessage = "Department name is required")]
        [StringLength(100, MinimumLength = 2,
            ErrorMessage = "Department name must be between 2 and 100 characters")]
        public string Name { get; set; } = string.Empty;

        public ICollection<User> Users { get; set; } = new List<User>();
    }
}