namespace TimeSheetAppWeb.Model.DTOs
{
    public class ProjectUpdateRequest
    {
        public string? ProjectName { get; set; }
        public string? Description { get; set; }
        public int? ManagerId { get; set; }
        public DateTime? StartDate { get; set; }
        public DateTime? EndDate { get; set; }
    }
}
