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

public class LeaveServiceTests
{
    private readonly Mock<IRepository<int, LeaveRequest>> _leaveRepoMock;
    private readonly Mock<IRepository<int, User>> _userRepoMock;
    private readonly Mock<IRepository<int, LeaveType>> _leaveTypeRepoMock;
    private readonly LeaveService _service;

    public LeaveServiceTests()
    {
        _leaveRepoMock = new Mock<IRepository<int, LeaveRequest>>();
        _userRepoMock = new Mock<IRepository<int, User>>();
        _leaveTypeRepoMock = new Mock<IRepository<int, LeaveType>>();

        _service = new LeaveService(
            _leaveRepoMock.Object,
            _userRepoMock.Object,
            _leaveTypeRepoMock.Object);
    }

    // ================= APPLY LEAVE =================

    [Fact]
    public async Task ApplyLeaveAsync_Should_Succeed()
    {
        var user = new User { Id = 1, Name = "John" };
        var leaveType = new LeaveType { Id = 1, Name = "Sick Leave" };

        _userRepoMock.Setup(r => r.GetByIdAsync(1))
                     .ReturnsAsync(user);

        _leaveTypeRepoMock.Setup(r => r.GetByIdAsync(1))
                          .ReturnsAsync(leaveType);

        _leaveRepoMock.Setup(r => r.GetAllAsync())
                      .ReturnsAsync(new List<LeaveRequest>());

        _leaveRepoMock.Setup(r => r.AddAsync(It.IsAny<LeaveRequest>()))
                      .ReturnsAsync((LeaveRequest l) => l);

        var request = new LeaveCreateRequest
        {
            LeaveTypeId = 1,
            FromDate = DateTime.Today,
            ToDate = DateTime.Today.AddDays(1),
            Reason = "Medical"
        };

        var result = await _service.ApplyLeaveAsync(1, request);

        Assert.True(result.Success);
        Assert.Equal("Leave applied successfully", result.Message);
        Assert.Equal("John", result.Data.EmployeeName);
    }

    [Fact]
    public async Task ApplyLeaveAsync_Should_Fail_When_User_Not_Found()
    {
        _userRepoMock.Setup(r => r.GetByIdAsync(1))
                     .ReturnsAsync((User)null);

        var result = await _service.ApplyLeaveAsync(1, new LeaveCreateRequest());

        Assert.False(result.Success);
        Assert.Equal("User not found", result.Message);
    }

    [Fact]
    public async Task ApplyLeaveAsync_Should_Fail_When_Overlapping()
    {
        var existingLeave = new LeaveRequest
        {
            UserId = 1,
            FromDate = DateTime.Today,
            ToDate = DateTime.Today.AddDays(3),
            Status = LeaveStatus.Pending
        };

        _userRepoMock.Setup(r => r.GetByIdAsync(1))
                     .ReturnsAsync(new User { Id = 1, Name = "John" });

        _leaveTypeRepoMock.Setup(r => r.GetByIdAsync(1))
                          .ReturnsAsync(new LeaveType { Id = 1, Name = "Sick" });

        _leaveRepoMock.Setup(r => r.GetAllAsync())
                      .ReturnsAsync(new List<LeaveRequest> { existingLeave });

        var request = new LeaveCreateRequest
        {
            LeaveTypeId = 1,
            FromDate = DateTime.Today,
            ToDate = DateTime.Today.AddDays(1)
        };

        var result = await _service.ApplyLeaveAsync(1, request);

        Assert.False(result.Success);
        Assert.Equal("You already have leave in the selected date range.", result.Message);
    }

    // ================= APPROVE / REJECT =================

    [Fact]
    public async Task ApproveOrRejectLeaveAsync_Should_Approve()
    {
        var leave = new LeaveRequest
        {
            Id = 1,
            UserId = 2,
            Status = LeaveStatus.Pending
        };

        _leaveRepoMock.Setup(r => r.GetByIdAsync(1))
                      .ReturnsAsync(leave);

        _leaveRepoMock.Setup(r => r.UpdateAsync(1, leave))
                      .ReturnsAsync(leave);

        var request = new LeaveApprovalRequest
        {
            LeaveId = 1,
            ApprovedById = 99,
            IsApproved = true
        };

        var result = await _service.ApproveOrRejectLeaveAsync(request);

        Assert.True(result.Success);
        Assert.True(result.Data);
        Assert.Equal("Leave approved", result.Message);
    }

    [Fact]
    public async Task ApproveOrRejectLeaveAsync_Should_Fail_If_Self_Approve()
    {
        var leave = new LeaveRequest
        {
            Id = 1,
            UserId = 10
        };

        _leaveRepoMock.Setup(r => r.GetByIdAsync(1))
                      .ReturnsAsync(leave);

        var request = new LeaveApprovalRequest
        {
            LeaveId = 1,
            ApprovedById = 10,
            IsApproved = true
        };

        var result = await _service.ApproveOrRejectLeaveAsync(request);

        Assert.False(result.Success);
        Assert.False(result.Data);
        Assert.Equal("You cannot approve your own leave.", result.Message);
    }

    // ================= GET USER LEAVES =================

    [Fact]
    public async Task GetUserLeavesAsync_Should_Return_Leaves()
    {
        var leave = new LeaveRequest
        {
            Id = 1,
            UserId = 1,
            LeaveTypeId = 1,
            FromDate = DateTime.Today,
            ToDate = DateTime.Today.AddDays(1),
            Status = LeaveStatus.Pending
        };

        _leaveRepoMock.Setup(r => r.GetAllAsync())
                      .ReturnsAsync(new List<LeaveRequest> { leave });

        _userRepoMock.Setup(r => r.GetByIdAsync(1))
                     .ReturnsAsync(new User { Id = 1, Name = "John" });

        _leaveTypeRepoMock.Setup(r => r.GetByIdAsync(1))
                          .ReturnsAsync(new LeaveType { Id = 1, Name = "Sick" });

        var result = await _service.GetUserLeavesAsync(1);

        Assert.True(result.Success);
        Assert.NotNull(result.Data);
        Assert.Single(result.Data!);
    }

    // ================= GET ALL LEAVES =================

    [Fact]
    public async Task GetAllLeavesAsync_Should_Return_All()
    {
        var leave = new LeaveRequest
        {
            Id = 1,
            UserId = 1,
            LeaveTypeId = 1,
            FromDate = DateTime.Today,
            ToDate = DateTime.Today.AddDays(1),
            Status = LeaveStatus.Pending
        };

        _leaveRepoMock.Setup(r => r.GetAllAsync())
                      .ReturnsAsync(new List<LeaveRequest> { leave });

        _userRepoMock.Setup(r => r.GetByIdAsync(1))
                     .ReturnsAsync(new User { Id = 1, Name = "John" });

        _leaveTypeRepoMock.Setup(r => r.GetByIdAsync(1))
                          .ReturnsAsync(new LeaveType { Id = 1, Name = "Sick" });

        var result = await _service.GetAllLeavesAsync();

        Assert.True(result.Success);
        Assert.NotNull(result.Data);
        Assert.Single(result.Data!);
    }
}