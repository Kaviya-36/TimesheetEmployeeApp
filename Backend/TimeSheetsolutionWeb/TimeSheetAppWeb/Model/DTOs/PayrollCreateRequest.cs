namespace TimeSheetAppWeb.Model.DTOs
{
    public class PayrollCreateRequest
    {
        public int UserId { get; set; }
        public decimal BasicSalary { get; set; }
        public decimal OvertimeAmount { get; set; }
        public decimal Deductions { get; set; }
        public DateTime SalaryMonth { get; set; }
    }
}
