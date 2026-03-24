using Xunit;
using Moq;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using TimeSheetAppWeb.Interface;
using TimeSheetAppWeb.Model;
using TimeSheetAppWeb.Model.DTOs;
using TimeSheetAppWeb.Services;

public class PayrollServiceTests
{
    private readonly Mock<IRepository<int, Payroll>> _payrollRepoMock;
    private readonly Mock<IRepository<int, User>> _userRepoMock;
    private readonly PayrollService _service;

    public PayrollServiceTests()
    {
        _payrollRepoMock = new Mock<IRepository<int, Payroll>>();
        _userRepoMock = new Mock<IRepository<int, User>>();

        _service = new PayrollService(
            _payrollRepoMock.Object,
            _userRepoMock.Object);
    }

    // ================= CREATE PAYROLL =================

    [Fact]
    public async Task CreatePayrollAsync_Should_Succeed()
    {
        var user = new User
        {
            Id = 1,
            Name = "John",
            EmployeeId = "EMP001"
        };

        var request = new PayrollCreateRequest
        {
            UserId = 1,
            BasicSalary = 10000,
            OvertimeAmount = 2000,
            Deductions = 1000,
            SalaryMonth = new DateTime(2025, 3, 1)
        };

        _userRepoMock.Setup(r => r.GetByIdAsync(1))
                     .ReturnsAsync(user);

        _payrollRepoMock.Setup(r => r.AddAsync(It.IsAny<Payroll>()))
                        .ReturnsAsync((Payroll p) =>
                        {
                            p.Id = 10;
                            return p;
                        });

        var result = await _service.CreatePayrollAsync(request);

        Assert.True(result.Success);
        Assert.Equal("Payroll created successfully", result.Message);
        Assert.Equal(11000, result.Data!.NetSalary);
        Assert.Equal("John", result.Data.EmployeeName);
    }

    [Fact]
    public async Task CreatePayrollAsync_Should_Fail_When_User_Not_Found()
    {
        _userRepoMock.Setup(r => r.GetByIdAsync(1))
                     .ReturnsAsync((User)null);

        var result = await _service.CreatePayrollAsync(new PayrollCreateRequest
        {
            UserId = 1
        });

        Assert.False(result.Success);
        Assert.Equal("User not found", result.Message);
    }

    // ================= GET PAYROLL BY ID =================

    [Fact]
    public async Task GetPayrollByIdAsync_Should_Succeed()
    {
        var payroll = new Payroll
        {
            Id = 1,
            UserId = 1,
            BasicSalary = 10000,
            OvertimeAmount = 1000,
            Deductions = 500,
            NetSalary = 10500,
            SalaryMonth = DateTime.Today,
            GeneratedDate = DateTime.Now
        };

        var user = new User
        {
            Id = 1,
            Name = "John",
            EmployeeId = "EMP001"
        };

        _payrollRepoMock.Setup(r => r.GetByIdAsync(1))
                        .ReturnsAsync(payroll);

        _userRepoMock.Setup(r => r.GetByIdAsync(1))
                     .ReturnsAsync(user);

        var result = await _service.GetPayrollByIdAsync(1);

        Assert.True(result.Success);
        Assert.Equal("John", result.Data!.EmployeeName);
        Assert.Equal(10500, result.Data.NetSalary);
    }

    [Fact]
    public async Task GetPayrollByIdAsync_Should_Fail_When_Not_Found()
    {
        _payrollRepoMock.Setup(r => r.GetByIdAsync(1))
                        .ReturnsAsync((Payroll)null);

        var result = await _service.GetPayrollByIdAsync(1);

        Assert.False(result.Success);
        Assert.Equal("Payroll not found", result.Message);
    }

    // ================= GET USER PAYROLLS =================

    [Fact]
    public async Task GetUserPayrollsAsync_Should_Return_Payrolls()
    {
        var payrollList = new List<Payroll>
        {
            new Payroll
            {
                Id = 1,
                UserId = 1,
                BasicSalary = 10000,
                OvertimeAmount = 0,
                Deductions = 0,
                NetSalary = 10000,
                SalaryMonth = new DateTime(2025, 3, 1),
                GeneratedDate = DateTime.Now
            }
        };

        var user = new User
        {
            Id = 1,
            Name = "John",
            EmployeeId = "EMP001"
        };

        _payrollRepoMock.Setup(r => r.GetAllAsync())
                        .ReturnsAsync(payrollList);

        _userRepoMock.Setup(r => r.GetByIdAsync(1))
                     .ReturnsAsync(user);

        var result = await _service.GetUserPayrollsAsync(1);

        Assert.True(result.Success);
        Assert.Single(result.Data!);
    }

    [Fact]
    public async Task GetUserPayrollsAsync_Should_Fail_When_No_Records()
    {
        _payrollRepoMock.Setup(r => r.GetAllAsync())
                        .ReturnsAsync(new List<Payroll>());

        var result = await _service.GetUserPayrollsAsync(1);

        Assert.False(result.Success);
        Assert.Equal("No payroll records found for this user", result.Message);
    }

    // ================= GET ALL PAYROLLS =================

    [Fact]
    public async Task GetAllPayrollsAsync_Should_Return_All()
    {
        var payrollList = new List<Payroll>
        {
            new Payroll
            {
                Id = 1,
                UserId = 1,
                BasicSalary = 10000,
                OvertimeAmount = 0,
                Deductions = 0,
                NetSalary = 10000,
                SalaryMonth = new DateTime(2025, 3, 1),
                GeneratedDate = DateTime.Now
            }
        };

        var user = new User
        {
            Id = 1,
            Name = "John",
            EmployeeId = "EMP001"
        };

        _payrollRepoMock.Setup(r => r.GetAllAsync())
                        .ReturnsAsync(payrollList);

        _userRepoMock.Setup(r => r.GetByIdAsync(1))
                     .ReturnsAsync(user);

        var result = await _service.GetAllPayrollsAsync();

        Assert.True(result.Success);
        Assert.Single(result.Data!);
    }

    [Fact]
    public async Task GetAllPayrollsAsync_Should_Fail_When_Empty()
    {
        _payrollRepoMock.Setup(r => r.GetAllAsync())
                        .ReturnsAsync(new List<Payroll>());

        var result = await _service.GetAllPayrollsAsync();

        Assert.False(result.Success);
        Assert.Equal("No payroll records found", result.Message);
    }
}