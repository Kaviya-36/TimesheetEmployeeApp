namespace TimeSheetAppWeb.Model.DTOs
{
    public class LeaveApprovalRequest
    {
        public int LeaveId { get; set; }           // Leave request ID
        public int ApprovedById { get; set; }      // Manager/Admin ID
        public bool IsApproved { get; set; }       // Approve or reject
        public string? ManagerComment { get; set; }
    }
}
