namespace TimeSheetAppWeb.Model.DTOs
{
    public class InternDetailsDto
    {
        public int Id { get; set; }
        public int UserId { get; set; }
        public string UserName { get; set; } = string.Empty;

        public DateTime TrainingStart { get; set; }
        public DateTime TrainingEnd { get; set; }

        public int? MentorId { get; set; }
        public string? MentorName { get; set; }

        public int TrainingDays => (int)(TrainingEnd - TrainingStart).TotalDays + 1;
    }
}
