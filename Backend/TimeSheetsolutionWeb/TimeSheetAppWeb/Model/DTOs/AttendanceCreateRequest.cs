namespace TimeSheetAppWeb.Model.DTOs
{
    public class AttendanceCreateRequest
    {
        public int UserId { get; set; }
        public DateTime Date { get; set; }
        public TimeSpan CheckIn { get; set; }
        public TimeSpan CheckOut { get; set; }
    }
}
