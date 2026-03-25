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

        public decimal BasicSalary { get; set; }

        public decimal OvertimeAmount { get; set; }


        public decimal Deductions { get; set; }

        public decimal NetSalary { get; set; }

        [Required(ErrorMessage = "Salary month is required")]
        public DateTime SalaryMonth { get; set; }

        public DateTime GeneratedDate { get; set; } = DateTime.UtcNow;
    }
}