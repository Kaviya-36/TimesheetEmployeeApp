public class AttendanceResponse
{
    public int Id { get; set; }
    public int UserId { get; set; }   // ✅ Add this
    public string EmployeeName { get; set; } = string.Empty;
    public DateTime Date { get; set; }
    public string? CheckIn { get; set; }
    public string? CheckOut { get; set; }
    public bool IsLate { get; set; }
    public string? TotalHours { get; set; }
}