using Xunit;
using Moq;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using TimeSheetAppWeb.Interface;
using TimeSheetAppWeb.Model;
using TimeSheetAppWeb.Model.DTOs;
using TimeSheetAppWeb.Services;

public class TimesheetServiceTests
{
    private readonly Mock<IRepository<int, Timesheet>> _timesheetRepoMock;
    private readonly Mock<IRepository<int, User>> _userRepoMock;
    private readonly Mock<IRepository<int, Project>> _projectRepoMock;
    private readonly Mock<ILogger<TimesheetService>> _loggerMock;
    private readonly TimesheetService _service;

    public TimesheetServiceTests()
    {
        _timesheetRepoMock = new Mock<IRepository<int, Timesheet>>();
        _userRepoMock = new Mock<IRepository<int, User>>();
        _projectRepoMock = new Mock<IRepository<int, Project>>();
        _loggerMock = new Mock<ILogger<TimesheetService>>();

        _service = new TimesheetService(
            _timesheetRepoMock.Object,
            _userRepoMock.Object,
            _projectRepoMock.Object,
            _loggerMock.Object);
    }

    // ================= CREATE =================

    [Fact]
    public async Task CreateTimesheetAsync_Should_Succeed()
    {
        var user = new User { Id = 1, Name = "John", EmployeeId = "EMP001" };
        var project = new Project { Id = 1, ProjectName = "Project1" };

        _userRepoMock.Setup(r => r.GetByIdAsync(1)).ReturnsAsync(user);
        _projectRepoMock.Setup(r => r.GetByIdAsync(1)).ReturnsAsync(project);
        _timesheetRepoMock.Setup(r => r.AddAsync(It.IsAny<Timesheet>()))
                          .ReturnsAsync((Timesheet t) => t);

        var request = new TimesheetCreateRequest
        {
            ProjectId = 1,
            ProjectName = "Project1",
            WorkDate = DateTime.Today,
            StartTime = TimeSpan.FromHours(9),     // ✅ fixed
            EndTime = TimeSpan.FromHours(17),
            BreakTime = TimeSpan.FromHours(1),
            TaskDescription = "Development"
        };

        var result = await _service.CreateTimesheetAsync(1, request);

        Assert.True(result.Success);
        Assert.Equal("Timesheet created successfully", result.Message);
        Assert.Equal(7, result.Data!.HoursWorked);
    }

    [Fact]
    public async Task CreateTimesheetAsync_Should_Fail_When_User_Not_Found()
    {
        _userRepoMock.Setup(r => r.GetByIdAsync(1))
                     .ReturnsAsync((User)null);

        var result = await _service.CreateTimesheetAsync(1, new TimesheetCreateRequest());

        Assert.False(result.Success);
        Assert.Equal("User not found", result.Message);
    }

    // ================= UPDATE =================

    [Fact]
    public async Task UpdateTimesheetAsync_Should_Succeed()
    {
        var timesheet = new Timesheet
        {
            Id = 1,
            UserId = 1,
            ProjectId = 1,
            StartTime = TimeSpan.FromHours(9),     // ✅ fixed
            EndTime = TimeSpan.FromHours(17),
            BreakTime = TimeSpan.FromHours(1),
            WorkDate = DateTime.Today
        };

        var user = new User { Id = 1, Name = "John", EmployeeId = "EMP001" };
        var project = new Project { Id = 1, ProjectName = "Project1" };

        _timesheetRepoMock.Setup(r => r.GetByIdAsync(1)).ReturnsAsync(timesheet);
        _timesheetRepoMock.Setup(r => r.UpdateAsync(1, timesheet)).ReturnsAsync(timesheet);
        _userRepoMock.Setup(r => r.GetByIdAsync(1)).ReturnsAsync(user);
        _projectRepoMock.Setup(r => r.GetByIdAsync(1)).ReturnsAsync(project);

        var request = new TimesheetUpdateRequest
        {
            TaskDescription = "Updated",
            Status = TimesheetStatus.Approved,
        };

        var result = await _service.UpdateTimesheetAsync(1, request);

        Assert.True(result.Success);
        Assert.Equal("Timesheet updated successfully", result.Message);
    }

    [Fact]
    public async Task UpdateTimesheetAsync_Should_Fail_When_Not_Found()
    {
        _timesheetRepoMock.Setup(r => r.GetByIdAsync(1))
                          .ReturnsAsync((Timesheet)null);

        var result = await _service.UpdateTimesheetAsync(1, new TimesheetUpdateRequest());

        Assert.False(result.Success);
        Assert.Equal("Timesheet not found", result.Message);
    }

    // ================= DELETE =================

    [Fact]
    public async Task DeleteTimesheetAsync_Should_Succeed()
    {
        var timesheet = new Timesheet { Id = 1 };

        _timesheetRepoMock.Setup(r => r.GetByIdAsync(1)).ReturnsAsync(timesheet);
        _timesheetRepoMock.Setup(r => r.DeleteAsync(1)).ReturnsAsync(timesheet);

        var result = await _service.DeleteTimesheetAsync(1);

        Assert.True(result.Success);
        Assert.True(result.Data);
    }

    // ================= GET USER TIMESHEETS =================

    [Fact]
    public async Task GetUserTimesheetsAsync_Should_Return_List()
    {
        var user = new User { Id = 1, Name = "John", EmployeeId = "EMP001" };
        var project = new Project { Id = 1, ProjectName = "Project1" };

        var timesheet = new Timesheet
        {
            Id = 1,
            UserId = 1,
            ProjectId = 1,
            WorkDate = DateTime.Today
        };

        _userRepoMock.Setup(r => r.GetByIdAsync(1)).ReturnsAsync(user);
        _timesheetRepoMock.Setup(r => r.GetAllAsync())
                          .ReturnsAsync(new List<Timesheet> { timesheet });
        _projectRepoMock.Setup(r => r.GetByIdAsync(1)).ReturnsAsync(project);

        var result = await _service.GetUserTimesheetsAsync(1);

        Assert.True(result.Success);
        Assert.Single(result.Data!);
    }

    // ================= APPROVE =================

    [Fact]
    public async Task ApproveOrRejectTimesheetAsync_Should_Approve()
    {
        var timesheet = new Timesheet
        {
            Id = 1,
            UserId = 2
        };

        _timesheetRepoMock.Setup(r => r.GetByIdAsync(1))
                          .ReturnsAsync(timesheet);
        _timesheetRepoMock.Setup(r => r.UpdateAsync(1, timesheet))
                          .ReturnsAsync(timesheet);

        var request = new TimesheetApprovalRequest
        {
            TimesheetId = 1,
            ApprovedById = 99,
            IsApproved = true
        };

        var result = await _service.ApproveOrRejectTimesheetAsync(request);

        Assert.True(result.Success);
        Assert.True(result.Data);
        Assert.Equal("Timesheet status updated", result.Message);
    }

    [Fact]
    public async Task ApproveOrRejectTimesheetAsync_Should_Fail_When_Self_Approve()
    {
        var timesheet = new Timesheet
        {
            Id = 1,
            UserId = 10
        };

        _timesheetRepoMock.Setup(r => r.GetByIdAsync(1))
                          .ReturnsAsync(timesheet);

        var request = new TimesheetApprovalRequest
        {
            TimesheetId = 1,
            ApprovedById = 10,
            IsApproved = true
        };

        var result = await _service.ApproveOrRejectTimesheetAsync(request);

        Assert.False(result.Success);
        Assert.False(result.Data);
        Assert.Equal("You cannot approve your own Timesheet.", result.Message);
    }
}