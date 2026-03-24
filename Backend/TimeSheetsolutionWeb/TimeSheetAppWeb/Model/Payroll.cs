namespace TimeSheetAppWeb.Model
{
    public class Payroll : IComparable<Payroll>, IEquatable<Payroll>
    {
        public int Id { get; set; }

        public int UserId { get; set; }
        public User? User { get; set; }

        public decimal BasicSalary { get; set; }
        public decimal OvertimeAmount { get; set; }
        public decimal Deductions { get; set; }
        public decimal NetSalary { get; set; }

        public DateTime SalaryMonth { get; set; }
        public DateTime GeneratedDate { get; set; } = DateTime.Now;
        public int CompareTo(Payroll? other)
        {
            if (other == null) return 1;

            int monthComparison = this.SalaryMonth.CompareTo(other.SalaryMonth);
            if (monthComparison != 0)
                return monthComparison;

            return this.UserId.CompareTo(other.UserId);
        }

        public bool Equals(Payroll? other)
        {
            if (other == null) return false;

            return this.Id == other.Id;
        }
    }
}
 
