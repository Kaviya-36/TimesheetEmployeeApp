namespace TimeSheetAppWeb.Model.DTOs
{
    public class InternDetailsCreateDto
    {
        public int UserId { get; set; }
        public DateTime TrainingStart { get; set; }
        public DateTime TrainingEnd { get; set; }
        public int? MentorId { get; set; }
    }
}