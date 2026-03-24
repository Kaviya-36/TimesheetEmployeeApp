namespace TimeSheetAppWeb.Model.DTOs
{
    public class UserUpdateRequest
    {
        public string? Name { get; set; }
        public string? Email { get; set; }
        public string? Phone { get; set; }
        public int? DepartmentId { get; set; }
        public string? Role { get; set; }
        public bool? IsActive { get; set; }
    }
}
