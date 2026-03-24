namespace TimeSheetAppWeb.Model.DTOs
{
    public class LeaveCreateRequest
    {
        public int LeaveTypeId { get; set; }
        public DateTime FromDate { get; set; }
        public DateTime ToDate { get; set; }
        public string? Reason { get; set; }
    }
}
