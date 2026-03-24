namespace TimeSheetAppWeb.Model.DTOs
{
    public class ProjectCreateRequest
    {
        public string ProjectName { get; set; } = string.Empty;
        public string? Description { get; set; }
        public int? ManagerId { get; set; }
        public DateTime? StartDate { get; set; }
        public DateTime? EndDate { get; set; }
    }
}