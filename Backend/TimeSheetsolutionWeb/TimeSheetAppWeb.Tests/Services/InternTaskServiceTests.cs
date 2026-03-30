using Microsoft.Extensions.Logging;
using Moq;
using TimeSheetAppWeb.Interface;
using TimeSheetAppWeb.Model;
using TimeSheetAppWeb.Model.DTOs;
using TimeSheetAppWeb.Services;
using Xunit;
using InternTaskStatus = TimeSheetAppWeb.Model.TaskStatus;

namespace TimeSheetAppWeb.Tests.Services
{
    public class InternTaskServiceTests
    {
        private readonly Mock<IRepository<int, InternTask>> _taskRepo = new();
        private readonly Mock<IRepository<int, User>>       _userRepo = new();
        private readonly Mock<ILogger<InternTaskService>>   _logger   = new();

        private InternTaskService CreateService() =>
            new(_taskRepo.Object, _userRepo.Object, _logger.Object);

        private User MakeIntern(int id = 1) => new User
        {
            Id = id, Name = "Intern", Email = "i@t.com",
            EmployeeId = "I001", Role = UserRole.Intern,
            PasswordHash = "h", IsActive = true
        };

        // ── CreateTaskAsync ────────────────────────────────────────────────────

        [Fact]
        public async Task CreateTask_NonMentor_ReturnsFail()
        {
            var svc = CreateService();
            var result = await svc.CreateTaskAsync(
                new InternTaskCreateRequest { InternId = 1, Title = "Task" },
                "Employee");

            Assert.False(result.Success);
            Assert.Contains("mentor", result.Message, StringComparison.OrdinalIgnoreCase);
        }

        [Fact]
        public async Task CreateTask_InternNotFound_ReturnsFail()
        {
            _userRepo.Setup(r => r.GetByIdAsync(1)).ReturnsAsync((User?)null);
            var svc = CreateService();

            var result = await svc.CreateTaskAsync(
                new InternTaskCreateRequest { InternId = 1, Title = "Task" },
                "Mentor");

            Assert.False(result.Success);
        }

        [Fact]
        public async Task CreateTask_UserNotIntern_ReturnsFail()
        {
            var manager = new User
            {
                Id = 1, Name = "Mgr", Email = "m@t.com",
                EmployeeId = "M001", Role = UserRole.Manager,
                PasswordHash = "h", IsActive = true
            };
            _userRepo.Setup(r => r.GetByIdAsync(1)).ReturnsAsync(manager);
            var svc = CreateService();

            var result = await svc.CreateTaskAsync(
                new InternTaskCreateRequest { InternId = 1, Title = "Task" },
                "Mentor");

            Assert.False(result.Success);
        }

        [Fact]
        public async Task CreateTask_Valid_ReturnsSuccess()
        {
            var intern = MakeIntern();
            _userRepo.Setup(r => r.GetByIdAsync(1)).ReturnsAsync(intern);
            _taskRepo.Setup(r => r.AddAsync(It.IsAny<InternTask>()))
                     .ReturnsAsync(new InternTask
                     {
                         Id = 1, InternId = 1, Title = "Task",
                         AssignedDate = DateTime.Now,
                         Status = InternTaskStatus.Pending
                     });

            var svc = CreateService();
            var result = await svc.CreateTaskAsync(
                new InternTaskCreateRequest { InternId = 1, Title = "Task", DueDate = DateTime.Today.AddDays(7) },
                "Mentor");

            Assert.True(result.Success);
            Assert.Equal("Task", result.Data?.Title);
        }

        // ── DeleteTaskAsync ────────────────────────────────────────────────────

        [Fact]
        public async Task DeleteTask_NotFound_ReturnsFail()
        {
            _taskRepo.Setup(r => r.GetByIdAsync(99)).ReturnsAsync((InternTask?)null);
            var svc = CreateService();

            var result = await svc.DeleteTaskAsync(99, "Mentor");

            Assert.False(result.Success);
        }

        [Fact]
        public async Task DeleteTask_Valid_ReturnsSuccess()
        {
            var task = new InternTask
            {
                Id = 1, InternId = 1, Title = "T",
                AssignedDate = DateTime.Now,
                Status = InternTaskStatus.Pending
            };
            _taskRepo.Setup(r => r.GetByIdAsync(1)).ReturnsAsync(task);
            _taskRepo.Setup(r => r.DeleteAsync(1)).ReturnsAsync(task);

            var svc = CreateService();
            var result = await svc.DeleteTaskAsync(1, "Mentor");

            Assert.True(result.Success);
        }

        // ── GetInternTasksAsync ────────────────────────────────────────────────

        [Fact]
        public async Task GetInternTasks_ReturnsOnlyInternTasks()
        {
            var intern = MakeIntern(1);
            var tasks = new List<InternTask>
            {
                new() { Id = 1, InternId = 1, Title = "T1", AssignedDate = DateTime.Now, Status = InternTaskStatus.Pending },
                new() { Id = 2, InternId = 2, Title = "T2", AssignedDate = DateTime.Now, Status = InternTaskStatus.Pending }
            };
            _taskRepo.Setup(r => r.GetAllAsync()).ReturnsAsync(tasks);
            _userRepo.Setup(r => r.GetByIdAsync(1)).ReturnsAsync(intern);

            var svc = CreateService();
            var result = await svc.GetInternTasksAsync(1, 1, 10);

            Assert.True(result.Success);
            Assert.Equal(1, result.Data?.TotalRecords);
        }

        // ── UpdateTaskAsync ────────────────────────────────────────────────────

        [Fact]
        public async Task UpdateTask_NonMentor_ReturnsFail()
        {
            var svc = CreateService();
            var result = await svc.UpdateTaskAsync(1, new InternTaskUpdateRequest { Title = "X" }, "Employee");

            Assert.False(result.Success);
            Assert.Contains("mentor", result.Message, StringComparison.OrdinalIgnoreCase);
        }

        [Fact]
        public async Task UpdateTask_NotFound_ReturnsFail()
        {
            _taskRepo.Setup(r => r.GetByIdAsync(99)).ReturnsAsync((InternTask?)null);
            var svc = CreateService();

            var result = await svc.UpdateTaskAsync(99, new InternTaskUpdateRequest { Title = "X" }, "Mentor");

            Assert.False(result.Success);
        }

        [Fact]
        public async Task UpdateTask_InternNotFound_ReturnsFail()
        {
            var task = new InternTask { Id = 1, InternId = 99, Title = "T", AssignedDate = DateTime.Now, Status = InternTaskStatus.Pending };
            _taskRepo.Setup(r => r.GetByIdAsync(1)).ReturnsAsync(task);
            _userRepo.Setup(r => r.GetByIdAsync(99)).ReturnsAsync((User?)null);
            var svc = CreateService();

            var result = await svc.UpdateTaskAsync(1, new InternTaskUpdateRequest { Title = "X" }, "Mentor");

            Assert.False(result.Success);
        }

        [Fact]
        public async Task UpdateTask_Valid_ReturnsSuccess()
        {
            var intern = MakeIntern();
            var task = new InternTask { Id = 1, InternId = 1, Title = "Old", AssignedDate = DateTime.Now, Status = InternTaskStatus.Pending };
            _taskRepo.Setup(r => r.GetByIdAsync(1)).ReturnsAsync(task);
            _userRepo.Setup(r => r.GetByIdAsync(1)).ReturnsAsync(intern);
            _taskRepo.Setup(r => r.UpdateAsync(1, It.IsAny<InternTask>())).ReturnsAsync(task);
            var svc = CreateService();

            var result = await svc.UpdateTaskAsync(1, new InternTaskUpdateRequest
            {
                Title = "New Title", Description = "Desc",
                DueDate = DateTime.Today.AddDays(7), Status = InternTaskStatus.InProgress
            }, "Mentor");

            Assert.True(result.Success);
            Assert.Equal("New Title", result.Data?.Title);
        }

        // ── MapToDto — null title branch ───────────────────────────────────────

        [Fact]
        public async Task CreateTask_NullTitle_MapsToEmptyString()
        {
            var intern = MakeIntern();
            _userRepo.Setup(r => r.GetByIdAsync(1)).ReturnsAsync(intern);
            _taskRepo.Setup(r => r.AddAsync(It.IsAny<InternTask>()))
                     .ReturnsAsync(new InternTask
                     {
                         Id = 1, InternId = 1, Title = null!,  // null title
                         AssignedDate = DateTime.Now,
                         Status = InternTaskStatus.Pending
                     });

            var svc = CreateService();
            var result = await svc.CreateTaskAsync(
                new InternTaskCreateRequest { InternId = 1, Title = null!, DueDate = DateTime.Today.AddDays(7) },
                "Mentor");

            Assert.True(result.Success);
            Assert.Equal(string.Empty, result.Data?.Title);
        }

        // ── DeleteTask — unauthorized role ─────────────────────────────────────

        [Fact]
        public async Task DeleteTask_UnauthorizedRole_ReturnsFail()
        {
            var svc = CreateService();
            var result = await svc.DeleteTaskAsync(1, "Employee");

            Assert.False(result.Success);
            Assert.Contains("HR", result.Message, StringComparison.OrdinalIgnoreCase);
        }
    }
}
