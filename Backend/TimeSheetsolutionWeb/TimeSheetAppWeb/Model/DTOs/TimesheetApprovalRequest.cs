namespace TimeSheetAppWeb.Model.DTOs
{
    public class TimesheetApprovalRequest
    {
        public int TimesheetId { get; set; }
        public int ApprovedById { get; set; } // Manager/Admin
        public bool IsApproved { get; set; }
        public string? ManagerComment { get; set; }
    }
}
