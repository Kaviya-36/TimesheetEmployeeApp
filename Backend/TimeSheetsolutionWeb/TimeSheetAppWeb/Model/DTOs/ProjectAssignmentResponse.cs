namespace TimeSheetAppWeb.Model.DTOs
{
    public class ProjectAssignmentResponse
    {
        public int Id { get; set; }
        public int ProjectId { get; set; }
        public string ProjectName { get; set; } = string.Empty;
        public int UserId { get; set; }
        public string UserName { get; set; } = string.Empty;
        public string UserEmail { get; set; } = string.Empty;
        public DateTime? EndDate { get; set; }
        public bool IsExpired { get; set; }
        public DateTime StartDate { get; set; }
    }
}
