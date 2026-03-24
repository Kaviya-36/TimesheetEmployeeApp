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
using TaskStatus = TimeSheetAppWeb.Model.TaskStatus;

public class InternTaskServiceTests
{
    private readonly Mock<IRepository<int, InternTask>> _taskRepoMock;
    private readonly Mock<IRepository<int, User>> _userRepoMock;
    private readonly InternTaskService _service;

    public InternTaskServiceTests()
    {
        _taskRepoMock = new Mock<IRepository<int, InternTask>>();
        _userRepoMock = new Mock<IRepository<int, User>>();
        _service = new InternTaskService(_taskRepoMock.Object, _userRepoMock.Object);
    }

    // ---------------- CREATE TASK ----------------
    [Fact]
    public async Task CreateTaskAsync_Should_Succeed_For_Mentor()
    {
        var intern = new User { Id = 1, Name = "Intern1", Role = UserRole.Intern };
        _userRepoMock.Setup(r => r.GetByIdAsync(1)).ReturnsAsync(intern);
        _taskRepoMock.Setup(r => r.AddAsync(It.IsAny<InternTask>())).ReturnsAsync((InternTask t) => t);

        var request = new InternTaskCreateRequest
        {
            InternId = 1,
            Title = "Task1",
            Description = "Desc1",
            DueDate = DateTime.Today.AddDays(1)
        };

        var result = await _service.CreateTaskAsync(request, "Mentor");

        Assert.True(result.Success);
        Assert.Equal("Task created successfully", result.Message);
        Assert.Equal("Intern1", result.Data.InternName);
    }

    [Fact]
    public async Task CreateTaskAsync_Should_Fail_If_Not_Mentor()
    {
        var result = await _service.CreateTaskAsync(new InternTaskCreateRequest(), "Intern");
        Assert.False(result.Success);
        Assert.Equal("Only mentors can create tasks.", result.Message);
    }

    [Fact]
    public async Task CreateTaskAsync_Should_Fail_If_Intern_Not_Found()
    {
        _userRepoMock.Setup(r => r.GetByIdAsync(2)).ReturnsAsync((User)null);
        var result = await _service.CreateTaskAsync(new InternTaskCreateRequest { InternId = 2 }, "Mentor");
        Assert.False(result.Success);
        Assert.Equal("Intern not found", result.Message);
    }

    [Fact]
    public async Task CreateTaskAsync_Should_Fail_If_User_Not_Intern()
    {
        var user = new User { Id = 3, Role = UserRole.Manager };
        _userRepoMock.Setup(r => r.GetByIdAsync(3)).ReturnsAsync(user);
        var result = await _service.CreateTaskAsync(new InternTaskCreateRequest { InternId = 3 }, "Mentor");
        Assert.False(result.Success);
        Assert.Equal("Only users with role 'Intern' can be assigned tasks.", result.Message);
    }

    // ---------------- UPDATE TASK ----------------
    [Fact]
    public async Task UpdateTaskAsync_Should_Succeed()
    {
        var task = new InternTask { Id = 1, InternId = 1, Title = "Old", Status = TaskStatus.Pending };
        var intern = new User { Id = 1, Name = "Intern1", Role = UserRole.Intern };

        _taskRepoMock.Setup(r => r.GetByIdAsync(1)).ReturnsAsync(task);
        _userRepoMock.Setup(r => r.GetByIdAsync(1)).ReturnsAsync(intern);
        _taskRepoMock.Setup(r => r.UpdateAsync(1, task))
             .ReturnsAsync(task);
        var request = new InternTaskUpdateRequest
        {
            Title = "New",
            Status = TaskStatus.Completed
        };

        var result = await _service.UpdateTaskAsync(1, request, "Mentor");

        Assert.True(result.Success);
        Assert.Equal("Task updated successfully", result.Message);
        Assert.Equal("New", result.Data.Title);
        Assert.Equal(TaskStatus.Completed, task.Status);
    }

    [Fact]
    public async Task UpdateTaskAsync_Should_Fail_If_Task_Not_Found()
    {
        _taskRepoMock.Setup(r => r.GetByIdAsync(99)).ReturnsAsync((InternTask)null);
        var result = await _service.UpdateTaskAsync(99, new InternTaskUpdateRequest(), "Mentor");
        Assert.False(result.Success);
        Assert.Equal("Task not found", result.Message);
    }

    [Fact]
    public async Task UpdateTaskAsync_Should_Fail_If_Not_Mentor()
    {
        var result = await _service.UpdateTaskAsync(1, new InternTaskUpdateRequest(), "Intern");
        Assert.False(result.Success);
        Assert.Equal("Only mentors can update tasks.", result.Message);
    }

    // ---------------- GET INTERN TASKS ----------------
    [Fact]
    public async Task GetInternTasksAsync_Should_Return_Tasks()
    {
        var tasks = new List<InternTask>
        {
            new InternTask { Id = 1, InternId = 1, Title = "T1", AssignedDate = DateTime.Now }
        };
        var user = new User { Id = 1, Name = "Intern1", Role = UserRole.Intern };

        _taskRepoMock.Setup(r => r.GetAllAsync()).ReturnsAsync(tasks);
        _userRepoMock.Setup(r => r.GetByIdAsync(1)).ReturnsAsync(user);

        var result = await _service.GetInternTasksAsync(1);

        Assert.True(result.Success);
        Assert.Single(result.Data);
        Assert.Equal("Intern1", result.Data.First().InternName);
    }

    // ---------------- DELETE TASK ----------------
    [Fact]
    public async Task DeleteTaskAsync_Should_Succeed_For_HR_Manager_Admin()
    {
        var task = new InternTask { Id = 1 };
        _taskRepoMock.Setup(r => r.GetByIdAsync(1)).ReturnsAsync(task);
        _taskRepoMock.Setup(r => r.DeleteAsync(1))
              .ReturnsAsync(task);

        var roles = new[] { "HR", "Manager", "Admin" };

        foreach (var role in roles)
        {
            var result = await _service.DeleteTaskAsync(1, role);
            Assert.True(result.Success);
            Assert.Equal("Task deleted successfully", result.Message);
            Assert.True(result.Data);
        }
    }

    [Fact]
    public async Task DeleteTaskAsync_Should_Fail_If_Not_Authorized()
    {
        var result = await _service.DeleteTaskAsync(1, "Intern");
        Assert.False(result.Success);
        Assert.Equal("Only HR, Manager, or Admin can delete tasks.", result.Message);
    }

    [Fact]
    public async Task DeleteTaskAsync_Should_Fail_If_Task_Not_Found()
    {
        _taskRepoMock.Setup(r => r.GetByIdAsync(100)).ReturnsAsync((InternTask)null);
        var result = await _service.DeleteTaskAsync(100, "Admin");
        Assert.False(result.Success);
        Assert.Equal("Task not found", result.Message);
    }
}