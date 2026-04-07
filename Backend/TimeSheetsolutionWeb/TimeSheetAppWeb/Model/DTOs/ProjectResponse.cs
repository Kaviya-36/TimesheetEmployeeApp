namespace TimeSheetAppWeb.Model.DTOs
{
    public class ProjectResponse
    {
        public int Id { get; set; }
        public string ProjectName { get; set; } = string.Empty;
        public string? Description { get; set; }
        public int? ManagerId { get; set; } 
        public string? ManagerName { get; set; }
        public DateTime? StartDate { get; set; }
        public DateTime? EndDate { get; set; }
        public bool IsExpired { get; set; }
        public string Status { get; set; } = string.Empty;
    }
}
