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

        // ── UpdateProjectAsync ─────────────────────────────────────────────────

        [Fact]
        public async Task UpdateProject_NotFound_ReturnsFail()
        {
            _prjRepo.Setup(r => r.GetByIdAsync(99)).ReturnsAsync((Project?)null);
            var svc = CreateService();

            var result = await svc.UpdateProjectAsync(99, new ProjectUpdateRequest { ProjectName = "X" });

            Assert.False(result.Success);
        }

        [Fact]
        public async Task UpdateProject_ManagerNotFound_ReturnsFail()
        {
            var project = MakeProject();
            _prjRepo.Setup(r => r.GetByIdAsync(1)).ReturnsAsync(project);
            _userRepo.Setup(r => r.GetByIdAsync(5)).ReturnsAsync((User?)null);
            var svc = CreateService();

            var result = await svc.UpdateProjectAsync(1, new ProjectUpdateRequest { ManagerId = 5 });

            Assert.False(result.Success);
            Assert.Contains("manager", result.Message, StringComparison.OrdinalIgnoreCase);
        }

        [Fact]
        public async Task UpdateProject_ManagerNotManagerRole_ReturnsFail()
        {
            var project = MakeProject();
            var employee = MakeUser(5, UserRole.Employee);
            _prjRepo.Setup(r => r.GetByIdAsync(1)).ReturnsAsync(project);
            _userRepo.Setup(r => r.GetByIdAsync(5)).ReturnsAsync(employee);
            var svc = CreateService();

            var result = await svc.UpdateProjectAsync(1, new ProjectUpdateRequest { ManagerId = 5 });

            Assert.False(result.Success);
        }

        [Fact]
        public async Task UpdateProject_Valid_ReturnsSuccess()
        {
            var project = MakeProject();
            var manager = MakeUser(5, UserRole.Manager);
            _prjRepo.Setup(r => r.GetByIdAsync(1)).ReturnsAsync(project);
            _userRepo.Setup(r => r.GetByIdAsync(5)).ReturnsAsync(manager);
            _prjRepo.Setup(r => r.UpdateAsync(1, It.IsAny<Project>())).ReturnsAsync(project);
            var svc = CreateService();

            var result = await svc.UpdateProjectAsync(1, new ProjectUpdateRequest
            {
                ProjectName = "Updated", ManagerId = 5,
                StartDate = DateTime.Today.AddDays(1), EndDate = DateTime.Today.AddDays(30)
            });

            Assert.True(result.Success);
        }

        // ── DeleteProjectAsync ─────────────────────────────────────────────────

        [Fact]
        public async Task DeleteProject_NotFound_ReturnsFail()
        {
            _prjRepo.Setup(r => r.GetByIdAsync(99)).ReturnsAsync((Project?)null);
            var svc = CreateService();

            var result = await svc.DeleteProjectAsync(99);

            Assert.False(result.Success);
        }

        [Fact]
        public async Task DeleteProject_Valid_ReturnsSuccess()
        {
            var project = MakeProject();
            _prjRepo.Setup(r => r.GetByIdAsync(1)).ReturnsAsync(project);
            _prjRepo.Setup(r => r.DeleteAsync(1)).ReturnsAsync(project);
            var svc = CreateService();

            var result = await svc.DeleteProjectAsync(1);

            Assert.True(result.Success);
            Assert.True(result.Data);
        }

        // ── GetProjectByIdAsync ────────────────────────────────────────────────

        [Fact]
        public async Task GetProjectById_NotFound_ReturnsFail()
        {
            _prjRepo.Setup(r => r.GetByIdAsync(99)).ReturnsAsync((Project?)null);
            var svc = CreateService();

            var result = await svc.GetProjectByIdAsync(99);

            Assert.False(result.Success);
        }

        [Fact]
        public async Task GetProjectById_WithManager_ReturnsManagerName()
        {
            var project = new Project { Id = 1, ProjectName = "P", StartDate = DateTime.Today, ManagerId = 5 };
            var manager = MakeUser(5, UserRole.Manager);
            _prjRepo.Setup(r => r.GetByIdAsync(1)).ReturnsAsync(project);
            _userRepo.Setup(r => r.GetByIdAsync(5)).ReturnsAsync(manager);
            var svc = CreateService();

            var result = await svc.GetProjectByIdAsync(1);

            Assert.True(result.Success);
            Assert.Equal("Test", result.Data?.ManagerName);
        }

        // ── CreateProject with ManagerId ───────────────────────────────────────

        [Fact]
        public async Task CreateProject_ManagerNotFound_ReturnsFail()
        {
            _prjRepo.Setup(r => r.GetAllAsync()).ReturnsAsync(new List<Project>());
            _userRepo.Setup(r => r.GetByIdAsync(5)).ReturnsAsync((User?)null);
            var svc = CreateService();

            var result = await svc.CreateProjectAsync(new ProjectCreateRequest
            {
                ProjectName = "P", StartDate = DateTime.Today.AddDays(1), ManagerId = 5
            });

            Assert.False(result.Success);
            Assert.Contains("manager", result.Message, StringComparison.OrdinalIgnoreCase);
        }

        [Fact]
        public async Task CreateProject_ManagerNotManagerRole_ReturnsFail()
        {
            _prjRepo.Setup(r => r.GetAllAsync()).ReturnsAsync(new List<Project>());
            _userRepo.Setup(r => r.GetByIdAsync(5)).ReturnsAsync(MakeUser(5, UserRole.Employee));
            var svc = CreateService();

            var result = await svc.CreateProjectAsync(new ProjectCreateRequest
            {
                ProjectName = "P", StartDate = DateTime.Today.AddDays(1), ManagerId = 5
            });

            Assert.False(result.Success);
        }

        [Fact]
        public async Task CreateProject_WithManager_AutoAssigns()
        {
            var manager = MakeUser(5, UserRole.Manager);
            var project = new Project { Id = 1, ProjectName = "P", StartDate = DateTime.Today.AddDays(1), ManagerId = 5 };
            _prjRepo.Setup(r => r.GetAllAsync()).ReturnsAsync(new List<Project>());
            _userRepo.Setup(r => r.GetByIdAsync(5)).ReturnsAsync(manager);
            _prjRepo.Setup(r => r.AddAsync(It.IsAny<Project>())).ReturnsAsync(project);
            _asnRepo.Setup(r => r.AddAsync(It.IsAny<ProjectAssignment>()))
                    .ReturnsAsync(new ProjectAssignment { Id = 1 });
            var svc = CreateService();

            var result = await svc.CreateProjectAsync(new ProjectCreateRequest
            {
                ProjectName = "P", StartDate = DateTime.Today.AddDays(1), ManagerId = 5
            });

            Assert.True(result.Success);
        }

        [Fact]
        public async Task CreateProject_EndDateBeforeStart_ReturnsFail()
        {
            _prjRepo.Setup(r => r.GetAllAsync()).ReturnsAsync(new List<Project>());
            var svc = CreateService();

            var result = await svc.CreateProjectAsync(new ProjectCreateRequest
            {
                ProjectName = "P",
                StartDate = DateTime.Today.AddDays(5),
                EndDate = DateTime.Today.AddDays(2)
            });

            Assert.False(result.Success);
        }

        // ── AssignUser — role check ────────────────────────────────────────────

        [Fact]
        public async Task AssignUser_InternRole_ReturnsFail()
        {
            var intern = MakeUser(1, UserRole.Intern);
            var project = MakeProject();
            _userRepo.Setup(r => r.GetByIdAsync(1)).ReturnsAsync(intern);
            _prjRepo.Setup(r => r.GetByIdAsync(1)).ReturnsAsync(project);
            _asnRepo.Setup(r => r.GetAllAsync()).ReturnsAsync(new List<ProjectAssignment>());
            var svc = CreateService();

            var result = await svc.AssignUserToProjectAsync(new ProjectAssignRequest
            {
                ProjectId = 1, UserId = 1, ProjectName = "P"
            });

            Assert.False(result.Success);
        }

        // ── RemoveUserFromProjectAsync ─────────────────────────────────────────

        [Fact]
        public async Task RemoveAssignment_NotFound_ReturnsFail()
        {
            _asnRepo.Setup(r => r.GetByIdAsync(99)).ReturnsAsync((ProjectAssignment?)null);
            var svc = CreateService();

            var result = await svc.RemoveUserFromProjectAsync(99);

            Assert.False(result.Success);
        }

        [Fact]
        public async Task RemoveAssignment_Valid_ReturnsSuccess()
        {
            var asn = new ProjectAssignment { Id = 1, ProjectId = 1, UserId = 1 };
            _asnRepo.Setup(r => r.GetByIdAsync(1)).ReturnsAsync(asn);
            _asnRepo.Setup(r => r.DeleteAsync(1)).ReturnsAsync(asn);
            var svc = CreateService();

            var result = await svc.RemoveUserFromProjectAsync(1);

            Assert.True(result.Success);
        }

        // ── GetAllProjectsAsync ────────────────────────────────────────────────

        [Fact]
        public async Task GetAllProjects_WithFilters_ReturnsFiltered()
        {
            var projects = new List<Project>
            {
                new() { Id = 1, ProjectName = "A", StartDate = DateTime.Today.AddDays(1), ManagerId = 5 },
                new() { Id = 2, ProjectName = "B", StartDate = DateTime.Today.AddDays(2) }
            };
            _prjRepo.Setup(r => r.GetAllAsync()).ReturnsAsync(projects);
            _userRepo.Setup(r => r.GetByIdAsync(5)).ReturnsAsync(MakeUser(5, UserRole.Manager));
            var svc = CreateService();

            var result = await svc.GetAllProjectsAsync(managerId: 5);

            Assert.True(result.Success);
            Assert.Single(result.Data!);
        }

        // ── GetProjectAssignmentsAsync ─────────────────────────────────────────

        [Fact]
        public async Task GetProjectAssignments_ReturnsAssignmentsForProject()
        {
            var asn = new ProjectAssignment { Id = 1, ProjectId = 1, UserId = 1 };
            var project = MakeProject();
            var user = MakeUser();
            _asnRepo.Setup(r => r.GetAllAsync()).ReturnsAsync(new List<ProjectAssignment> { asn });
            _userRepo.Setup(r => r.GetByIdAsync(1)).ReturnsAsync(user);
            _prjRepo.Setup(r => r.GetByIdAsync(1)).ReturnsAsync(project);
            var svc = CreateService();

            var result = await svc.GetProjectAssignmentsAsync(1);

            Assert.True(result.Success);
            Assert.Single(result.Data!);
        }

        // ── GetMyProjectsAsync ─────────────────────────────────────────────────

        [Fact]
        public async Task GetMyProjects_ReturnsAssignedAndManagedProjects()
        {
            var user = MakeUser(1, UserRole.Manager);
            var assignedProject = MakeProject(1);
            var managedProject = new Project { Id = 2, ProjectName = "Managed", StartDate = DateTime.Today, ManagerId = 1 };
            var asn = new ProjectAssignment { Id = 1, ProjectId = 1, UserId = 1 };

            _asnRepo.Setup(r => r.GetAllAsync()).ReturnsAsync(new List<ProjectAssignment> { asn });
            _prjRepo.Setup(r => r.GetAllAsync()).ReturnsAsync(new List<Project> { assignedProject, managedProject });
            _prjRepo.Setup(r => r.GetByIdAsync(1)).ReturnsAsync(assignedProject);
            _userRepo.Setup(r => r.GetByIdAsync(1)).ReturnsAsync(user);
            var svc = CreateService();

            var result = await svc.GetMyProjectsAsync(1);

            Assert.True(result.Success);
            Assert.Equal(2, result.Data!.Count());
        }
    }
}
