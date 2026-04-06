using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace TimeSheetAppWeb.Model
{
    public class Payroll
    {
        [Key]
        public int Id { get; set; }

        [Required]
        public int UserId { get; set; }

        public User? User { get; set; }

        [Precision(18, 2)]
        public decimal BasicSalary { get; set; }

        [Precision(18, 2)]
        public decimal OvertimeAmount { get; set; }

        [Precision(18, 2)]
        public decimal Deductions { get; set; }

        // Daily rate derived from BasicSalary / working days in month
        [Precision(18, 2)]
        public decimal DailyRate { get; set; }

        // Extra pay for weekend days worked (1x daily rate per weekend day, total = 2x)
        [Precision(18, 2)]
        public decimal WeekendBonus { get; set; }

        [Precision(18, 2)]
        public decimal NetSalary { get; set; }

        [Required(ErrorMessage = "Salary month is required")]
        public DateTime SalaryMonth { get; set; }

        public DateTime GeneratedDate { get; set; } = DateTime.UtcNow;
    }
}