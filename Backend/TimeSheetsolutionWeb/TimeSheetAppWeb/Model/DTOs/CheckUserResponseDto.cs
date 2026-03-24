namespace TimeSheetAppWeb.Models.DTOs
{
    public class CheckUserResponseDto
    {
        public int UserId { get; set; }
        public string Username { get; set; } = string.Empty;
        public string Token { get; set; } = string.Empty;
        public string Role {  get; set; } = string.Empty;
    }
}
