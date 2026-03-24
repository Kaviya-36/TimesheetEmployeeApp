namespace TimeSheetAppWeb.Model.DTOs
{
    public class UserCreateRequest
    {
        public string EmployeeId { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public string Password { get; set; } = string.Empty;
        public string Phone { get; set; } = string.Empty;
        public int? DepartmentId { get; set; }
        public string Role { get; set; } = "Employee";
    }
}
