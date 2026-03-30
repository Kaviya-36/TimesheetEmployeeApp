using Microsoft.Extensions.Logging;
using Moq;
using TimeSheetAppWeb.Interface;
using TimeSheetAppWeb.Model;
using TimeSheetAppWeb.Model.DTOs;
using TimeSheetAppWeb.Services;
using Xunit;

namespace TimeSheetAppWeb.Tests.Services
{
    /// <summary>
    /// Unit tests for <see cref="PayrollService"/>.
    /// Covers payroll creation validation, net salary calculation, and retrieval.
    /// </summary>
    public class PayrollServiceTests
    {
        // ── Mocks ──────────────────────────────────────────────────────────────

        private readonly Mock<IRepository<int, Payroll>> _payRepo  = new();
        private readonly Mock<IRepository<int, User>>    _userRepo = new();
        private readonly Mock<ILogger<PayrollService>>   _logger   = new();

        // ── Helpers ────────────────────────────────────────────────────────────

        private PayrollService CreateService() =>
            new(_payRepo.Object, _userRepo.Object, _logger.Object);

        private static User MakeUser(int id = 1) => new()
        {
            Id           = id,
            Name         = "Test User",
            Email        = "test@example.com",
            EmployeeId   = "E001",
            Role         = UserRole.Employee,
            PasswordHash = "hashed",
            IsActive     = true
        };

        private static PayrollCreateRequest MakeRequest(
            decimal basicSalary  = 50_000m,
            decimal overtime     = 0m,
            decimal deductions   = 500m) =>
            new()
            {
                UserId         = 1,
                BasicSalary    = basicSalary,
                OvertimeAmount = overtime,
                Deductions     = deductions,
                SalaryMonth    = new DateTime(DateTime.Today.Year, DateTime.Today.Month, 1)
            };

        // ── CreatePayrollAsync ─────────────────────────────────────────────────

        [Fact]
        public async Task CreatePayroll_UserNotFound_ReturnsFail()
        {
            _userRepo.Setup(r => r.GetByIdAsync(1)).ReturnsAsync((User?)null);

            var result = await CreateService().CreatePayrollAsync(MakeRequest());

            Assert.False(result.Success);
        }

        [Fact]
        public async Task CreatePayroll_FutureMonth_ReturnsFail()
        {
            _userRepo.Setup(r => r.GetByIdAsync(1)).ReturnsAsync(MakeUser());
            var req = MakeRequest();
            req.SalaryMonth = DateTime.Today.AddMonths(2);

            var result = await CreateService().CreatePayrollAsync(req);

            Assert.False(result.Success);
            Assert.Contains("current month", result.Message, StringComparison.OrdinalIgnoreCase);
        }

        [Fact]
        public async Task CreatePayroll_DuplicateForSameMonth_ReturnsFail()
        {
            var currentMonth = new DateTime(DateTime.Today.Year, DateTime.Today.Month, 1);
            var existing     = new Payroll
            {
                Id          = 1,
                UserId      = 1,
                SalaryMonth = currentMonth,
                BasicSalary = 50_000m,
                NetSalary   = 49_500m
            };
            _userRepo.Setup(r => r.GetByIdAsync(1)).ReturnsAsync(MakeUser());
            _payRepo.Setup(r => r.GetAllAsync()).ReturnsAsync(new List<Payroll> { existing });

            var result = await CreateService().CreatePayrollAsync(MakeRequest());

            Assert.False(result.Success);
            Assert.Contains("already", result.Message, StringComparison.OrdinalIgnoreCase);
        }

        [Fact]
        public async Task CreatePayroll_Valid_CalculatesNetSalaryCorrectly()
        {
            // Net = BasicSalary + Overtime - Deductions = 50000 + 500 - 1000 = 49500
            const decimal expectedNet = 49_500m;

            _userRepo.Setup(r => r.GetByIdAsync(1)).ReturnsAsync(MakeUser());
            _payRepo.Setup(r => r.GetAllAsync()).ReturnsAsync(new List<Payroll>());
            _payRepo.Setup(r => r.AddAsync(It.IsAny<Payroll>()))
                    .ReturnsAsync((Payroll p) => { p.Id = 1; return p; });

            var result = await CreateService().CreatePayrollAsync(
                MakeRequest(basicSalary: 50_000m, overtime: 500m, deductions: 1_000m));

            Assert.True(result.Success);
            Assert.Equal(expectedNet, result.Data?.NetSalary);
        }

        [Fact]
        public async Task CreatePayroll_NegativeNetSalary_ReturnsFail()
        {
            _userRepo.Setup(r => r.GetByIdAsync(1)).ReturnsAsync(MakeUser());
            _payRepo.Setup(r => r.GetAllAsync()).ReturnsAsync(new List<Payroll>());

            // Deductions exceed basic salary → net would be negative
            var result = await CreateService().CreatePayrollAsync(
                MakeRequest(basicSalary: 1_000m, overtime: 0m, deductions: 5_000m));

            Assert.False(result.Success);
        }
    }
}
