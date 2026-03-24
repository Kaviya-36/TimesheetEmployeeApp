using TimeSheetAppWeb.Model;
using TaskStatusEnum = TimeSheetAppWeb.Model.TaskStatus;

namespace TimeSheetAppWeb.Model.DTOs
{
    public class InternTaskUpdateRequest
    {
        public string? Title { get; set; }              
        public string? Description { get; set; }       
        public TaskStatusEnum Status { get; set; }
        public DateTime? DueDate { get; set; }         
    }
}