namespace TimeSheetAppWeb.Model.DTOs
{
    public class PayrollResponse
    {
        public int PayrollId { get; set; }
        public string EmployeeName { get; set; } = string.Empty;
        public string EmployeeId { get; set; } = string.Empty;

        public decimal BasicSalary { get; set; }
        public decimal OvertimeAmount { get; set; }
        public decimal Deductions { get; set; }
        public decimal NetSalary { get; set; }

        public DateTime SalaryMonth { get; set; }
        public DateTime GeneratedDate { get; set; }
    }
}
