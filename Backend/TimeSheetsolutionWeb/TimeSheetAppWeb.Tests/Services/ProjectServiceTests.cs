using Microsoft.Extensions.Logging;
using Moq;
using TimeSheetAppWeb.Interface;
using TimeSheetAppWeb.Model;
using TimeSheetAppWeb.Model.DTOs;
using TimeSheetAppWeb.Services;
using Xunit;

namespace TimeSheetAppWeb.Tests.Services
{
    public class ProjectServiceTests
    {
        private readonly Mock<IRepository<int, Project>>           _prjRepo  = new();
        private readonly Mock<IRepository<int, User>>              _userRepo = new();
        private readonly Mock<IRepository<int, ProjectAssignment>> _asnRepo  = new();
        private readonly Mock<ILogger<ProjectService>>             _logger   = new();

        // Constructor order: project, user, assignment, logger
        private ProjectService CreateService() =>
            new(_prjRepo.Object, _userRepo.Object, _asnRepo.Object, _logger.Object);

        private User MakeUser(int id = 1, UserRole role = UserRole.Employee) => new User
        {
            Id = id, Name = "Test", Email = "t@t.com",
            EmployeeId = "E001", Role = role,
            PasswordHash = "h", IsActive = true
        };

        private Project MakeProject(int id = 1) => new Project
        {
            Id = id, ProjectName = "Test Project", StartDate = DateTime.Today.AddDays(1)
        };

        // ── CreateProjectAsync ─────────────────────────────────────────────────

        [Fact]
        public async Task CreateProject_Valid_ReturnsSuccess()
        {
            _prjRepo.Setup(r => r.GetAllAsync()).ReturnsAsync(new List<Project>());
            _prjRepo.Setup(r => r.AddAsync(It.IsAny<Project>())).ReturnsAsync(MakeProject());

            var svc = CreateService();
            var result = await svc.CreateProjectAsync(new ProjectCreateRequest
            {
                ProjectName = "New Project",
                StartDate   = DateTime.Today.AddDays(1)
            });

            Assert.True(result.Success);
        }

        [Fact]
        public async Task CreateProject_PastStartDate_ReturnsFail()
        {
            _prjRepo.Setup(r => r.GetAllAsync()).ReturnsAsync(new List<Project>());
            var svc = CreateService();

            var result = await svc.CreateProjectAsync(new ProjectCreateRequest
            {
                ProjectName = "Old Project",
                StartDate   = DateTime.Today.AddDays(-5)
            });

            Assert.False(result.Success);
            Assert.Contains("past", result.Message, StringComparison.OrdinalIgnoreCase);
        }

        // ── AssignUserToProjectAsync ───────────────────────────────────────────

        [Fact]
        public async Task AssignUser_UserNotFound_ReturnsFail()
        {
            _userRepo.Setup(r => r.GetByIdAsync(1)).ReturnsAsync((User?)null);
            _prjRepo.Setup(r => r.GetByIdAsync(1)).ReturnsAsync(MakeProject());
            var svc = CreateService();

            var result = await svc.AssignUserToProjectAsync(new ProjectAssignRequest
            {
                ProjectId = 1, UserId = 1, ProjectName = "P"
            });

            Assert.False(result.Success);
        }

        [Fact]
        public async Task AssignUser_ProjectNotFound_ReturnsFail()
        {
            _userRepo.Setup(r => r.GetByIdAsync(1)).ReturnsAsync(MakeUser());
            _prjRepo.Setup(r => r.GetByIdAsync(1)).ReturnsAsync((Project?)null);
            var svc = CreateService();

            var result = await svc.AssignUserToProjectAsync(new ProjectAssignRequest
            {
                ProjectId = 1, UserId = 1, ProjectName = "P"
            });

            Assert.False(result.Success);
        }

        [Fact]
        public async Task AssignUser_AlreadyAssigned_ReturnsFail()
        {
            var user    = MakeUser();
            var project = MakeProject();
            var existing = new ProjectAssignment { Id = 1, UserId = 1, ProjectId = 1 };

            _userRepo.Setup(r => r.GetByIdAsync(1)).ReturnsAsync(user);
            _prjRepo.Setup(r => r.GetByIdAsync(1)).ReturnsAsync(project);
            _asnRepo.Setup(r => r.GetAllAsync()).ReturnsAsync(new List<ProjectAssignment> { existing });

            var svc = CreateService();
            var result = await svc.AssignUserToProjectAsync(new ProjectAssignRequest
            {
                ProjectId = 1, UserId = 1, ProjectName = "Test Project"
            });

            Assert.False(result.Success);
            Assert.Contains("already", result.Message, StringComparison.OrdinalIgnoreCase);
        }

        [Fact]
        public async Task AssignUser_Valid_ReturnsSuccess()
        {
            var user    = MakeUser();
            var project = MakeProject();

            _userRepo.Setup(r => r.GetByIdAsync(1)).ReturnsAsync(user);
            _prjRepo.Setup(r => r.GetByIdAsync(1)).ReturnsAsync(project);
            _asnRepo.Setup(r => r.GetAllAsync()).ReturnsAsync(new List<ProjectAssignment>());
            _asnRepo.Setup(r => r.AddAsync(It.IsAny<ProjectAssignment>()))
                    .ReturnsAsync(new ProjectAssignment { Id = 1, UserId = 1, ProjectId = 1 });

            var svc = CreateService();
            var result = await svc.AssignUserToProjectAsync(new ProjectAssignRequest
            {
                ProjectId = 1, UserId = 1, ProjectName = "Test Project"
            });

            Assert.True(result.Success);
        }

        // ── GetUserProjectAssignmentsAsync ─────────────────────────────────────

        [Fact]
        public async Task GetUserAssignments_IncludesManagerOwnedProjects()
        {
            var manager = MakeUser(5, UserRole.Manager);
            var project = new Project { Id = 10, ProjectName = "Mgr Project", StartDate = DateTime.Today, ManagerId = 5 };

            _userRepo.Setup(r => r.GetByIdAsync(5)).ReturnsAsync(manager);
            _asnRepo.Setup(r => r.GetAllAsync()).ReturnsAsync(new List<ProjectAssignment>());
            _prjRepo.Setup(r => r.GetAllAsync()).ReturnsAsync(new List<Project> { project });

            var svc = CreateService();
            var result = await svc.GetUserProjectAssignmentsAsync(5);

            Assert.True(result.Success);
            Assert.Single(result.Data!);
            Assert.Equal("Mgr Project", result.Data!.First().ProjectName);
        }

        [Fact]
        public async Task GetUserAssignments_UserNotFound_ReturnsFail()
        {
            _userRepo.Setup(r => r.GetByIdAsync(99)).ReturnsAsync((User?)null);
            var svc = CreateService();

            var result = await svc.GetUserProjectAssignmentsAsync(99);

            Assert.False(result.Success);
        }
    }
}
