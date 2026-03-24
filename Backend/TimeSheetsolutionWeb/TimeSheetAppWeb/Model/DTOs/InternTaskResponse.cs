namespace TimeSheetAppWeb.Model.DTOs
{
    public class InternTaskResponse
    {
        public int Id { get; set; }
        public int UserId { get; set; }
        public string InternName { get; set; }=string.Empty;
        public string Title { get; set; }= string.Empty;    
        public string? Description { get; set; }
        public DateTime AssignedDate { get; set; }
        public DateTime? DueDate { get; set; }
    }
}
