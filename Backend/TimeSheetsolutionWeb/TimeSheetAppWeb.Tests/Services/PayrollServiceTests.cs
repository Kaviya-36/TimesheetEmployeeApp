using Microsoft.Extensions.Logging;
using Moq;
using TimeSheetAppWeb.Interface;
using TimeSheetAppWeb.Model;
using TimeSheetAppWeb.Model.DTOs;
using TimeSheetAppWeb.Services;
using Xunit;

namespace TimeSheetAppWeb.Tests.Services
{
    public class PayrollServiceTests
    {
        private readonly Mock<IRepository<int, Payroll>> _payRepo  = new();
        private readonly Mock<IRepository<int, User>>    _userRepo = new();
        private readonly Mock<ILogger<PayrollService>>   _logger   = new();

        private PayrollService CreateService() =>
            new(_payRepo.Object, _userRepo.Object, _logger.Object);

        private User MakeUser(int id = 1) => new User
        {
            Id = id, Name = "Test", Email = "t@t.com",
            EmployeeId = "E001", Role = UserRole.Employee,
            PasswordHash = "h", IsActive = true
        };

        private PayrollCreateRequest MakeRequest(decimal basic = 50000, decimal overtime = 0, decimal deductions = 500)
            => new PayrollCreateRequest
            {
                UserId = 1, BasicSalary = basic, OvertimeAmount = overtime,
                Deductions = deductions,
                SalaryMonth = new DateTime(DateTime.Today.Year, DateTime.Today.Month, 1)
            };

        // ── CreatePayrollAsync ─────────────────────────────────────────────────

        [Fact]
        public async Task CreatePayroll_UserNotFound_ReturnsFail()
        {
            _userRepo.Setup(r => r.GetByIdAsync(1)).ReturnsAsync((User?)null);
            var svc = CreateService();

            var result = await svc.CreatePayrollAsync(MakeRequest());

            Assert.False(result.Success);
        }

        [Fact]
        public async Task CreatePayroll_FutureMonth_ReturnsFail()
        {
            _userRepo.Setup(r => r.GetByIdAsync(1)).ReturnsAsync(MakeUser());
            var svc = CreateService();

            var req = MakeRequest();
            req.SalaryMonth = DateTime.Today.AddMonths(2);

            var result = await svc.CreatePayrollAsync(req);

            Assert.False(result.Success);
            Assert.Contains("current month", result.Message, StringComparison.OrdinalIgnoreCase);
        }

        [Fact]
        public async Task CreatePayroll_DuplicateMonth_ReturnsFail()
        {
            var user = MakeUser();
            var currentMonth = new DateTime(DateTime.Today.Year, DateTime.Today.Month, 1);
            var existing = new Payroll
            {
                Id = 1, UserId = 1, SalaryMonth = currentMonth,
                BasicSalary = 50000, NetSalary = 49500
            };
            _userRepo.Setup(r => r.GetByIdAsync(1)).ReturnsAsync(user);
            _payRepo.Setup(r => r.GetAllAsync()).ReturnsAsync(new List<Payroll> { existing });

            var svc = CreateService();
            var result = await svc.CreatePayrollAsync(MakeRequest());

            Assert.False(result.Success);
            Assert.Contains("already", result.Message, StringComparison.OrdinalIgnoreCase);
        }

        [Fact]
        public async Task CreatePayroll_Valid_CalculatesNetSalaryCorrectly()
        {
            var user = MakeUser();
            _userRepo.Setup(r => r.GetByIdAsync(1)).ReturnsAsync(user);
            _payRepo.Setup(r => r.GetAllAsync()).ReturnsAsync(new List<Payroll>());
            _payRepo.Setup(r => r.AddAsync(It.IsAny<Payroll>()))
                    .ReturnsAsync((Payroll p) => { p.Id = 1; return p; });

            var svc = CreateService();
            var result = await svc.CreatePayrollAsync(MakeRequest(50000, 500, 1000));

            Assert.True(result.Success);
            Assert.Equal(49500, result.Data?.NetSalary); // 50000 + 500 - 1000
        }

        [Fact]
        public async Task CreatePayroll_NegativeNetSalary_ReturnsFail()
        {
            var user = MakeUser();
            _userRepo.Setup(r => r.GetByIdAsync(1)).ReturnsAsync(user);
            _payRepo.Setup(r => r.GetAllAsync()).ReturnsAsync(new List<Payroll>());

            var svc = CreateService();
            var result = await svc.CreatePayrollAsync(MakeRequest(1000, 0, 5000));

            Assert.False(result.Success);
        }
    }
}
