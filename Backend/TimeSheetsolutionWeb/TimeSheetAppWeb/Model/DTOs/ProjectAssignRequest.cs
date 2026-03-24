namespace TimeSheetAppWeb.Model.DTOs
{
    public class ProjectAssignRequest
    {
        public int ProjectId { get; set; }
        public int UserId { get; set; }
        public string ProjectName { get; set; }
    }
}
