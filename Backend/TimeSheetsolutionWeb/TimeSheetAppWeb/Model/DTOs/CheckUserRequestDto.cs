using System.ComponentModel.DataAnnotations;

namespace TimeSheetAppWeb.Models.DTOs
{
    public class CheckUserRequestDto
    {
        [Required(ErrorMessage = "Username is required.")]
        public string Username { get; set; } = string.Empty;
        [Required(ErrorMessage = "Password is required.")]
        [MinLength(6, ErrorMessage = "Password must be at least 6 characters long.")]
        public string Password { get; set; } = string.Empty;
    }
}
