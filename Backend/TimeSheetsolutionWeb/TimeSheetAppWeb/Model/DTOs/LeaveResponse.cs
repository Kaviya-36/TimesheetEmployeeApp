namespace TimeSheetAppWeb.Model.DTOs
{
    public class LeaveResponse
    {
        public int Id { get; set; }
        public int UserId { get; set; }
        public string EmployeeName { get; set; } = string.Empty;
        public string LeaveType { get; set; } = string.Empty;
        public DateTime FromDate { get; set; }
        public DateTime ToDate { get; set; }
        public string? Reason { get; set; }
        public LeaveStatus Status { get; set; }
        public int? ApprovedById { get; set; }
        public DateTime? ApprovedDate { get; set; }
        public string? ManagerComment { get; set; }
        public int RemainingLeaves { get; set; }
    }
}