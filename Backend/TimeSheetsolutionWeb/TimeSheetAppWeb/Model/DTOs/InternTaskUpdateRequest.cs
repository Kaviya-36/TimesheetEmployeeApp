using TimeSheetAppWeb.Model;
using TaskStatusEnum = TimeSheetAppWeb.Model.TaskStatus;

namespace TimeSheetAppWeb.Model.DTOs
{
    public class InternTaskUpdateRequest
    {
        private string? _title;

        public string? Title
        {
            get => _title;
            set => _title = value;
        }

        // Accept "taskTitle" from frontend (same backing field as Title)
        public string? TaskTitle
        {
            get => _title;
            set { if (!string.IsNullOrEmpty(value)) _title = value; }
        }

        public string? Description { get; set; }
        public TaskStatusEnum Status { get; set; }
        public DateTime? DueDate { get; set; }
    }
}
