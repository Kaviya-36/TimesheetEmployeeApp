namespace TimeSheetAppWeb.Model.DTOs
{
    public class InternTaskCreateRequest
    {
        public int InternId { get; set; }

        // Accept both "title" and "taskTitle" from frontend
        private string _title = string.Empty;
        public string Title
        {
            get => _title;
            set => _title = value;
        }
        public string? TaskTitle
        {
            get => _title;
            set { if (!string.IsNullOrEmpty(value)) _title = value; }
        }

        public string? Description { get; set; }
        public DateTime AssignedDate { get; set; }
        public DateTime? DueDate { get; set; }
    }
}
