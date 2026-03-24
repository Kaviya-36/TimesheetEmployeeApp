namespace TimeSheetAppWeb.Model.DTOs
{
    public class LoginResponse
    {
        public int UserId { get; set; }
        public string Name { get; set; } = string.Empty;
        public string EmployeeId { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public string? Role { get; set; }
        public string? Token { get; set; }
    }
}
