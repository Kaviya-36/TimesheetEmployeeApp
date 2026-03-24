namespace TimeSheetAppWeb.Model.DTOs
{
    public class InternTaskCreateRequest
    {
        public int InternId { get; set; }
        public string Title { get; set; } = string.Empty;
        public string? Description { get; set; }
        public DateTime AssignedDate { get; set; }
        public DateTime? DueDate { get; set; }
    }
}
