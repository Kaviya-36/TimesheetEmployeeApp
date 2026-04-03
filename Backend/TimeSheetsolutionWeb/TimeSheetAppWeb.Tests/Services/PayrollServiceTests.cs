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

    public class PayrollServiceEdgeCaseTests
    {
        private readonly Mock<IRepository<int, Payroll>> _payRepo  = new();
        private readonly Mock<IRepository<int, User>>    _userRepo = new();
        private readonly Mock<ILogger<PayrollService>>   _logger   = new();

        private PayrollService CreateService() =>
            new(_payRepo.Object, _userRepo.Object, _logger.Object);

        private static User MakeUser(int id = 1) => new()
        {
            Id = id, Name = "Test User", Email = "test@example.com",
            EmployeeId = "E001", Role = UserRole.Employee,
            PasswordHash = "hashed", IsActive = true
        };

        private static PayrollCreateRequest MakeRequest(
            decimal basicSalary = 50_000m, decimal overtime = 0m, decimal deductions = 500m) =>
            new()
            {
                UserId = 1, BasicSalary = basicSalary, OvertimeAmount = overtime,
                Deductions = deductions,
                SalaryMonth = new DateTime(DateTime.Today.Year, DateTime.Today.Month, 1)
            };

        // ── CreatePayroll — past month ────────────────────────────────────────

        [Fact]
        public async Task CreatePayroll_PastMonth_ReturnsFail()
        {
            _userRepo.Setup(r => r.GetByIdAsync(1)).ReturnsAsync(MakeUser());
            var req = MakeRequest();
            req.SalaryMonth = DateTime.Today.AddMonths(-1);

            var result = await CreateService().CreatePayrollAsync(req);

            Assert.False(result.Success);
            Assert.Contains("current month", result.Message, StringComparison.OrdinalIgnoreCase);
        }

        // ── CreatePayroll — zero salary ───────────────────────────────────────

        [Fact]
        public async Task CreatePayroll_ZeroBasicSalary_CreatesWithZeroNet()
        {
            _userRepo.Setup(r => r.GetByIdAsync(1)).ReturnsAsync(MakeUser());
            _payRepo.Setup(r => r.GetAllAsync()).ReturnsAsync(new List<Payroll>());
            _payRepo.Setup(r => r.AddAsync(It.IsAny<Payroll>()))
                    .ReturnsAsync((Payroll p) => { p.Id = 1; return p; });

            var result = await CreateService().CreatePayrollAsync(
                MakeRequest(basicSalary: 0m, overtime: 0m, deductions: 0m));

            Assert.True(result.Success);
            Assert.Equal(0m, result.Data?.NetSalary);
        }

        // ── CreatePayroll — overtime only ─────────────────────────────────────

        [Fact]
        public async Task CreatePayroll_OvertimeOnly_NetEqualsOvertime()
        {
            _userRepo.Setup(r => r.GetByIdAsync(1)).ReturnsAsync(MakeUser());
            _payRepo.Setup(r => r.GetAllAsync()).ReturnsAsync(new List<Payroll>());
            _payRepo.Setup(r => r.AddAsync(It.IsAny<Payroll>()))
                    .ReturnsAsync((Payroll p) => { p.Id = 1; return p; });

            var result = await CreateService().CreatePayrollAsync(
                MakeRequest(basicSalary: 0m, overtime: 2_000m, deductions: 0m));

            Assert.True(result.Success);
            Assert.Equal(2_000m, result.Data?.NetSalary);
        }

        // ── GetPayrollById — not found ────────────────────────────────────────

        [Fact]
        public async Task GetPayrollById_NotFound_ReturnsFail()
        {
            _payRepo.Setup(r => r.GetByIdAsync(99)).ReturnsAsync((Payroll?)null);

            var result = await CreateService().GetPayrollByIdAsync(99);

            Assert.False(result.Success);
            Assert.Contains("not found", result.Message, StringComparison.OrdinalIgnoreCase);
        }

        [Fact]
        public async Task GetPayrollById_UserNotFound_ReturnsFail()
        {
            var payroll = new Payroll
            {
                Id = 1, UserId = 99, BasicSalary = 50_000m, NetSalary = 49_500m,
                SalaryMonth = new DateTime(DateTime.Today.Year, DateTime.Today.Month, 1)
            };
            _payRepo.Setup(r => r.GetByIdAsync(1)).ReturnsAsync(payroll);
            _userRepo.Setup(r => r.GetByIdAsync(99)).ReturnsAsync((User?)null);

            var result = await CreateService().GetPayrollByIdAsync(1);

            Assert.False(result.Success);
        }

        [Fact]
        public async Task GetPayrollById_Valid_ReturnsMappedDto()
        {
            var user = MakeUser();
            var payroll = new Payroll
            {
                Id = 1, UserId = 1, BasicSalary = 50_000m, OvertimeAmount = 500m,
                Deductions = 1_000m, NetSalary = 49_500m,
                SalaryMonth = new DateTime(DateTime.Today.Year, DateTime.Today.Month, 1),
                GeneratedDate = DateTime.Now
            };
            _payRepo.Setup(r => r.GetByIdAsync(1)).ReturnsAsync(payroll);
            _userRepo.Setup(r => r.GetByIdAsync(1)).ReturnsAsync(user);

            var result = await CreateService().GetPayrollByIdAsync(1);

            Assert.True(result.Success);
            Assert.Equal("Test User", result.Data?.EmployeeName);
            Assert.Equal(49_500m, result.Data?.NetSalary);
        }

        // ── GetUserPayrolls — no records ──────────────────────────────────────

        [Fact]
        public async Task GetUserPayrolls_NoRecords_ReturnsFail()
        {
            _payRepo.Setup(r => r.GetAllAsync()).ReturnsAsync(new List<Payroll>());

            var result = await CreateService().GetUserPayrollsAsync(1);

            Assert.False(result.Success);
            Assert.Contains("no payroll", result.Message, StringComparison.OrdinalIgnoreCase);
        }

        [Fact]
        public async Task GetUserPayrolls_WithDateFilter_ReturnsFiltered()
        {
            var user = MakeUser();
            var payrolls = new List<Payroll>
            {
                new() { Id = 1, UserId = 1, BasicSalary = 50_000m, NetSalary = 50_000m,
                        SalaryMonth = new DateTime(DateTime.Today.Year, DateTime.Today.Month, 1) },
                new() { Id = 2, UserId = 1, BasicSalary = 50_000m, NetSalary = 50_000m,
                        SalaryMonth = new DateTime(DateTime.Today.Year - 1, 1, 1) }
            };
            _payRepo.Setup(r => r.GetAllAsync()).ReturnsAsync(payrolls);
            _userRepo.Setup(r => r.GetByIdAsync(1)).ReturnsAsync(user);

            var result = await CreateService().GetUserPayrollsAsync(
                1, fromMonth: new DateTime(DateTime.Today.Year, 1, 1));

            Assert.True(result.Success);
            Assert.Single(result.Data!);
        }

        // ── GetAllPayrolls — no records ───────────────────────────────────────

        [Fact]
        public async Task GetAllPayrolls_NoRecords_ReturnsFail()
        {
            _payRepo.Setup(r => r.GetAllAsync()).ReturnsAsync(new List<Payroll>());

            var result = await CreateService().GetAllPayrollsAsync();

            Assert.False(result.Success);
        }

        [Fact]
        public async Task GetAllPayrolls_WithUserIdFilter_ReturnsOnlyThatUser()
        {
            var user1 = MakeUser(1);
            var user2 = MakeUser(2);
            var payrolls = new List<Payroll>
            {
                new() { Id = 1, UserId = 1, BasicSalary = 50_000m, NetSalary = 50_000m,
                        SalaryMonth = new DateTime(DateTime.Today.Year, DateTime.Today.Month, 1) },
                new() { Id = 2, UserId = 2, BasicSalary = 60_000m, NetSalary = 60_000m,
                        SalaryMonth = new DateTime(DateTime.Today.Year, DateTime.Today.Month, 1) }
            };
            _payRepo.Setup(r => r.GetAllAsync()).ReturnsAsync(payrolls);
            _userRepo.Setup(r => r.GetByIdAsync(1)).ReturnsAsync(user1);
            _userRepo.Setup(r => r.GetByIdAsync(2)).ReturnsAsync(user2);

            var result = await CreateService().GetAllPayrollsAsync(userId: 1);

            Assert.True(result.Success);
            Assert.Single(result.Data!);
        }

        // ── CreatePayroll — maps all fields to DTO ────────────────────────────

        [Fact]
        public async Task CreatePayroll_MapsEmployeeIdAndName()
        {
            var user = MakeUser();
            _userRepo.Setup(r => r.GetByIdAsync(1)).ReturnsAsync(user);
            _payRepo.Setup(r => r.GetAllAsync()).ReturnsAsync(new List<Payroll>());
            _payRepo.Setup(r => r.AddAsync(It.IsAny<Payroll>()))
                    .ReturnsAsync((Payroll p) => { p.Id = 1; return p; });

            var result = await CreateService().CreatePayrollAsync(MakeRequest());

            Assert.True(result.Success);
            Assert.Equal("Test User", result.Data?.EmployeeName);
            Assert.Equal("E001", result.Data?.EmployeeId);
        }
    }

    public class PayrollServiceAdditionalTests
    {
        private readonly Mock<IRepository<int, Payroll>> _payRepo  = new();
        private readonly Mock<IRepository<int, User>>    _userRepo = new();
        private readonly Mock<ILogger<PayrollService>>   _logger   = new();

        private PayrollService CreateService() =>
            new(_payRepo.Object, _userRepo.Object, _logger.Object);

        private static User MakeUser(int id = 1) => new()
        {
            Id = id, Name = "Test User", Email = "test@example.com",
            EmployeeId = "E001", Role = UserRole.Employee,
            PasswordHash = "hashed", IsActive = true
        };

        private static PayrollCreateRequest MakeRequest(
            decimal basicSalary = 50_000m, decimal overtime = 0m, decimal deductions = 500m) =>
            new()
            {
                UserId = 1, BasicSalary = basicSalary, OvertimeAmount = overtime,
                Deductions = deductions,
                SalaryMonth = new DateTime(DateTime.Today.Year, DateTime.Today.Month, 1)
            };

        // ── CreatePayrollAsync: user not found returns failure ────────────────

        [Fact]
        public async Task CreatePayroll_UserNotFound_MessageContainsUserNotFound()
        {
            _userRepo.Setup(r => r.GetByIdAsync(1)).ReturnsAsync((User?)null);

            var result = await CreateService().CreatePayrollAsync(MakeRequest());

            Assert.False(result.Success);
            Assert.Contains("user", result.Message, StringComparison.OrdinalIgnoreCase);
        }

        // ── CreatePayrollAsync: duplicate month+user returns failure ──────────

        [Fact]
        public async Task CreatePayroll_DuplicateMonthUser_MessageContainsAlready()
        {
            var currentMonth = new DateTime(DateTime.Today.Year, DateTime.Today.Month, 1);
            var existing = new Payroll
            {
                Id = 1, UserId = 1, SalaryMonth = currentMonth,
                BasicSalary = 50_000m, NetSalary = 49_500m
            };
            _userRepo.Setup(r => r.GetByIdAsync(1)).ReturnsAsync(MakeUser());
            _payRepo.Setup(r => r.GetAllAsync()).ReturnsAsync(new List<Payroll> { existing });

            var result = await CreateService().CreatePayrollAsync(MakeRequest());

            Assert.False(result.Success);
            Assert.Contains("already", result.Message, StringComparison.OrdinalIgnoreCase);
        }

        // ── CreatePayrollAsync: calculates net salary correctly ───────────────

        [Fact]
        public async Task CreatePayroll_NetSalary_IsBasicPlusOvertimeMinusDeductions()
        {
            _userRepo.Setup(r => r.GetByIdAsync(1)).ReturnsAsync(MakeUser());
            _payRepo.Setup(r => r.GetAllAsync()).ReturnsAsync(new List<Payroll>());
            _payRepo.Setup(r => r.AddAsync(It.IsAny<Payroll>()))
                    .ReturnsAsync((Payroll p) => { p.Id = 1; return p; });

            // 60000 + 2000 - 3000 = 59000
            var result = await CreateService().CreatePayrollAsync(
                MakeRequest(basicSalary: 60_000m, overtime: 2_000m, deductions: 3_000m));

            Assert.True(result.Success);
            Assert.Equal(59_000m, result.Data?.NetSalary);
        }

        // ── GetPayrollByIdAsync: not found returns failure ────────────────────

        [Fact]
        public async Task GetPayrollById_NotFound_MessageContainsNotFound()
        {
            _payRepo.Setup(r => r.GetByIdAsync(99)).ReturnsAsync((Payroll?)null);

            var result = await CreateService().GetPayrollByIdAsync(99);

            Assert.False(result.Success);
            Assert.Contains("not found", result.Message, StringComparison.OrdinalIgnoreCase);
        }

        // ── GetUserPayrollsAsync: returns user's payrolls ─────────────────────

        [Fact]
        public async Task GetUserPayrolls_ReturnsOnlyUserPayrolls()
        {
            var user = MakeUser();
            var payrolls = new List<Payroll>
            {
                new() { Id = 1, UserId = 1, BasicSalary = 50_000m, NetSalary = 50_000m,
                        SalaryMonth = new DateTime(DateTime.Today.Year, DateTime.Today.Month, 1) },
                new() { Id = 2, UserId = 2, BasicSalary = 60_000m, NetSalary = 60_000m,
                        SalaryMonth = new DateTime(DateTime.Today.Year, DateTime.Today.Month, 1) }
            };
            _payRepo.Setup(r => r.GetAllAsync()).ReturnsAsync(payrolls);
            _userRepo.Setup(r => r.GetByIdAsync(1)).ReturnsAsync(user);

            var result = await CreateService().GetUserPayrollsAsync(1);

            Assert.True(result.Success);
            Assert.Single(result.Data!);
        }

        // ── GetAllPayrollsAsync: returns all paged ────────────────────────────

        [Fact]
        public async Task GetAllPayrolls_ReturnsPaged()
        {
            var user = MakeUser();
            var payrolls = Enumerable.Range(1, 6).Select(i => new Payroll
            {
                Id = i, UserId = 1, BasicSalary = 50_000m, NetSalary = 50_000m,
                SalaryMonth = new DateTime(DateTime.Today.Year, DateTime.Today.Month, 1)
            }).ToList();
            _payRepo.Setup(r => r.GetAllAsync()).ReturnsAsync(payrolls);
            _userRepo.Setup(r => r.GetByIdAsync(1)).ReturnsAsync(user);

            var result = await CreateService().GetAllPayrollsAsync(pageNumber: 1, pageSize: 4);

            Assert.True(result.Success);
            Assert.Equal(4, result.Data!.Count());
        }

        // ── CreatePayrollAsync: no deductions, net equals basic + overtime ────

        [Fact]
        public async Task CreatePayroll_NoDeductions_NetEqualsBasicPlusOvertime()
        {
            _userRepo.Setup(r => r.GetByIdAsync(1)).ReturnsAsync(MakeUser());
            _payRepo.Setup(r => r.GetAllAsync()).ReturnsAsync(new List<Payroll>());
            _payRepo.Setup(r => r.AddAsync(It.IsAny<Payroll>()))
                    .ReturnsAsync((Payroll p) => { p.Id = 1; return p; });

            var result = await CreateService().CreatePayrollAsync(
                MakeRequest(basicSalary: 40_000m, overtime: 5_000m, deductions: 0m));

            Assert.True(result.Success);
            Assert.Equal(45_000m, result.Data?.NetSalary);
        }

        // ── GetAllPayrolls: min/max salary filter ─────────────────────────────

        [Fact]
        public async Task GetAllPayrolls_MinSalaryFilter_ExcludesLowerSalaries()
        {
            var user = MakeUser();
            var payrolls = new List<Payroll>
            {
                new() { Id = 1, UserId = 1, BasicSalary = 30_000m, NetSalary = 30_000m,
                        SalaryMonth = new DateTime(DateTime.Today.Year, DateTime.Today.Month, 1) },
                new() { Id = 2, UserId = 1, BasicSalary = 60_000m, NetSalary = 60_000m,
                        SalaryMonth = new DateTime(DateTime.Today.Year, DateTime.Today.Month, 1) }
            };
            _payRepo.Setup(r => r.GetAllAsync()).ReturnsAsync(payrolls);
            _userRepo.Setup(r => r.GetByIdAsync(1)).ReturnsAsync(user);

            var result = await CreateService().GetAllPayrollsAsync(minSalary: 50_000m);

            Assert.True(result.Success);
            Assert.Single(result.Data!);
        }
    }
