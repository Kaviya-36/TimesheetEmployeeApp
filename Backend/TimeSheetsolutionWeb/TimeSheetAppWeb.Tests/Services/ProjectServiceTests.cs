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

        // ── GetProjectByIdAsync — removed (method was deleted) ────────────────

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

        // ── GetMyProjectsAsync — removed (method was deleted) ─────────────────
    }
}

    public class ProjectServiceEdgeCaseTests
    {
        private readonly Mock<IRepository<int, Project>>           _prjRepo  = new();
        private readonly Mock<IRepository<int, User>>              _userRepo = new();
        private readonly Mock<IRepository<int, ProjectAssignment>> _asnRepo  = new();
        private readonly Mock<ILogger<ProjectService>>             _logger   = new();

        private ProjectService CreateService() =>
            new(_prjRepo.Object, _userRepo.Object, _asnRepo.Object, _logger.Object);

        private User MakeUser(int id = 1, UserRole role = UserRole.Employee, bool active = true) => new User
        {
            Id = id, Name = "Test", Email = "t@t.com",
            EmployeeId = "E001", Role = role,
            PasswordHash = "h", IsActive = active
        };

        private Project MakeProject(int id = 1) => new Project
        {
            Id = id, ProjectName = "Test Project", StartDate = DateTime.Today.AddDays(1)
        };

        // ── AssignUser — inactive user ────────────────────────────────────────

        [Fact]
        public async Task AssignUser_InactiveUser_ReturnsFail()
        {
            var inactiveUser = MakeUser(1, UserRole.Employee, active: false);
            var project = MakeProject();
            _userRepo.Setup(r => r.GetByIdAsync(1)).ReturnsAsync(inactiveUser);
            _prjRepo.Setup(r => r.GetByIdAsync(1)).ReturnsAsync(project);
            _asnRepo.Setup(r => r.GetAllAsync()).ReturnsAsync(new List<ProjectAssignment>());

            var svc = CreateService();
            var result = await svc.AssignUserToProjectAsync(new ProjectAssignRequest
            {
                ProjectId = 1, UserId = 1, ProjectName = "Test Project"
            });

            Assert.False(result.Success);
            Assert.Contains("inactive", result.Message, StringComparison.OrdinalIgnoreCase);
        }

        // ── AssignUser — HR role not allowed ──────────────────────────────────

        [Fact]
        public async Task AssignUser_HrRole_ReturnsFail()
        {
            var hrUser = MakeUser(1, UserRole.HR);
            var project = MakeProject();
            _userRepo.Setup(r => r.GetByIdAsync(1)).ReturnsAsync(hrUser);
            _prjRepo.Setup(r => r.GetByIdAsync(1)).ReturnsAsync(project);
            _asnRepo.Setup(r => r.GetAllAsync()).ReturnsAsync(new List<ProjectAssignment>());

            var svc = CreateService();
            var result = await svc.AssignUserToProjectAsync(new ProjectAssignRequest
            {
                ProjectId = 1, UserId = 1, ProjectName = "Test Project"
            });

            Assert.False(result.Success);
            Assert.Contains("Employee or Manager", result.Message, StringComparison.OrdinalIgnoreCase);
        }

        
        // ── CreateProject — today as start date (boundary) ───────────────────

        [Fact]
        public async Task CreateProject_TodayStartDate_Succeeds()
        {
            var project = MakeProject();
            project.StartDate = DateTime.Today;
            _prjRepo.Setup(r => r.GetAllAsync()).ReturnsAsync(new List<Project>());
            _prjRepo.Setup(r => r.AddAsync(It.IsAny<Project>())).ReturnsAsync(project);

            var svc = CreateService();
            var result = await svc.CreateProjectAsync(new ProjectCreateRequest
            {
                ProjectName = "Today Project",
                StartDate   = DateTime.Today
            });

            Assert.True(result.Success);
        }

        // ── GetAllProjects — date range filters ───────────────────────────────

        [Fact]
        public async Task GetAllProjects_StartFromFilter_ExcludesOlderProjects()
        {
            var projects = new List<Project>
            {
                new() { Id = 1, ProjectName = "Old", StartDate = DateTime.Today.AddDays(1) },
                new() { Id = 2, ProjectName = "New", StartDate = DateTime.Today.AddDays(10) }
            };
            _prjRepo.Setup(r => r.GetAllAsync()).ReturnsAsync(projects);
            var svc = CreateService();

            var result = await svc.GetAllProjectsAsync(startFrom: DateTime.Today.AddDays(5));

            Assert.True(result.Success);
            Assert.Single(result.Data!);
            Assert.Equal("New", result.Data!.First().ProjectName);
        }

        [Fact]
        public async Task GetAllProjects_EndToFilter_ExcludesProjectsWithLaterEndDate()
        {
            var projects = new List<Project>
            {
                new() { Id = 1, ProjectName = "Short", StartDate = DateTime.Today.AddDays(1), EndDate = DateTime.Today.AddDays(30) },
                new() { Id = 2, ProjectName = "Long",  StartDate = DateTime.Today.AddDays(1), EndDate = DateTime.Today.AddDays(90) }
            };
            _prjRepo.Setup(r => r.GetAllAsync()).ReturnsAsync(projects);
            var svc = CreateService();

            var result = await svc.GetAllProjectsAsync(endTo: DateTime.Today.AddDays(60));

            Assert.True(result.Success);
            Assert.Single(result.Data!);
            Assert.Equal("Short", result.Data!.First().ProjectName);
        }

        // ── GetAllProjects — pagination ───────────────────────────────────────

        [Fact]
        public async Task GetAllProjects_Pagination_ReturnsCorrectPage()
        {
            var projects = Enumerable.Range(1, 8).Select(i => new Project
            {
                Id = i, ProjectName = $"Project {i}", StartDate = DateTime.Today.AddDays(i)
            }).ToList();
            _prjRepo.Setup(r => r.GetAllAsync()).ReturnsAsync(projects);
            var svc = CreateService();

            var result = await svc.GetAllProjectsAsync(pageNumber: 2, pageSize: 3);

            Assert.True(result.Success);
            Assert.Equal(3, result.Data!.Count());
        }

        // ── GetProjectAssignments — pagination ────────────────────────────────

        [Fact]
        public async Task GetProjectAssignments_NoAssignments_ReturnsEmpty()
        {
            _asnRepo.Setup(r => r.GetAllAsync()).ReturnsAsync(new List<ProjectAssignment>());
            var svc = CreateService();

            var result = await svc.GetProjectAssignmentsAsync(1);

            Assert.True(result.Success);
            Assert.Empty(result.Data!);
        }

        // ── UpdateProject — only name updated ────────────────────────────────

        [Fact]
        public async Task UpdateProject_OnlyNameProvided_UpdatesNameOnly()
        {
            var project = MakeProject();
            _prjRepo.Setup(r => r.GetByIdAsync(1)).ReturnsAsync(project);
            _prjRepo.Setup(r => r.UpdateAsync(1, It.IsAny<Project>()))
                    .ReturnsAsync((int _, Project p) => p);

            var svc = CreateService();
            var result = await svc.UpdateProjectAsync(1, new ProjectUpdateRequest { ProjectName = "Renamed" });

            Assert.True(result.Success);
            Assert.Equal("Renamed", result.Data?.ProjectName);
        }

        // ── GetUserProjectAssignments — no assignments, no managed projects ───

        [Fact]
        public async Task GetUserAssignments_NoAssignmentsOrManagedProjects_ReturnsEmpty()
        {
            var user = MakeUser(1, UserRole.Employee);
            _userRepo.Setup(r => r.GetByIdAsync(1)).ReturnsAsync(user);
            _asnRepo.Setup(r => r.GetAllAsync()).ReturnsAsync(new List<ProjectAssignment>());
            _prjRepo.Setup(r => r.GetAllAsync()).ReturnsAsync(new List<Project>());

            var svc = CreateService();
            var result = await svc.GetUserProjectAssignmentsAsync(1);

            Assert.True(result.Success);
            Assert.Empty(result.Data!);
        }
    }

    public class ProjectServiceAdditionalTests
    {
        private readonly Mock<IRepository<int, Project>>           _prjRepo  = new();
        private readonly Mock<IRepository<int, User>>              _userRepo = new();
        private readonly Mock<IRepository<int, ProjectAssignment>> _asnRepo  = new();
        private readonly Mock<ILogger<ProjectService>>             _logger   = new();

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

        // ── CreateProjectAsync: success maps all fields ───────────────────────

        [Fact]
        public async Task CreateProject_MapsAllFieldsToResponse()
        {
            var project = new Project
            {
                Id = 1, ProjectName = "Full Project",
                Description = "Desc", StartDate = DateTime.Today.AddDays(1),
                EndDate = DateTime.Today.AddDays(30)
            };
            _prjRepo.Setup(r => r.GetAllAsync()).ReturnsAsync(new List<Project>());
            _prjRepo.Setup(r => r.AddAsync(It.IsAny<Project>())).ReturnsAsync(project);

            var result = await CreateService().CreateProjectAsync(new ProjectCreateRequest
            {
                ProjectName = "Full Project",
                Description = "Desc",
                StartDate   = DateTime.Today.AddDays(1),
                EndDate     = DateTime.Today.AddDays(30)
            });

            Assert.True(result.Success);
            Assert.Equal("Full Project", result.Data?.ProjectName);
            Assert.Equal("Desc", result.Data?.Description);
        }

        // ── UpdateProjectAsync: project not found returns failure ─────────────

        [Fact]
        public async Task UpdateProject_NotFound_MessageContainsNotFound()
        {
            _prjRepo.Setup(r => r.GetByIdAsync(99)).ReturnsAsync((Project?)null);

            var result = await CreateService().UpdateProjectAsync(99, new ProjectUpdateRequest { ProjectName = "X" });

            Assert.False(result.Success);
            Assert.Contains("not found", result.Message, StringComparison.OrdinalIgnoreCase);
        }

        // ── DeleteProjectAsync: project not found returns failure ─────────────

        [Fact]
        public async Task DeleteProject_NotFound_MessageContainsNotFound()
        {
            _prjRepo.Setup(r => r.GetByIdAsync(99)).ReturnsAsync((Project?)null);

            var result = await CreateService().DeleteProjectAsync(99);

            Assert.False(result.Success);
            Assert.Contains("not found", result.Message, StringComparison.OrdinalIgnoreCase);
        }

        // ── AssignUserToProjectAsync: user not found returns failure ──────────

        [Fact]
        public async Task AssignUser_UserNotFound_MessageContainsUserNotFound()
        {
            _prjRepo.Setup(r => r.GetByIdAsync(1)).ReturnsAsync(MakeProject());
            _userRepo.Setup(r => r.GetByIdAsync(1)).ReturnsAsync((User?)null);

            var result = await CreateService().AssignUserToProjectAsync(
                new ProjectAssignRequest { ProjectId = 1, UserId = 1, ProjectName = "P" });

            Assert.False(result.Success);
            Assert.Contains("user", result.Message, StringComparison.OrdinalIgnoreCase);
        }

        // ── AssignUserToProjectAsync: project not found returns failure ───────

        [Fact]
        public async Task AssignUser_ProjectNotFound_MessageContainsProjectNotFound()
        {
            _prjRepo.Setup(r => r.GetByIdAsync(1)).ReturnsAsync((Project?)null);
            _userRepo.Setup(r => r.GetByIdAsync(1)).ReturnsAsync(MakeUser());

            var result = await CreateService().AssignUserToProjectAsync(
                new ProjectAssignRequest { ProjectId = 1, UserId = 1, ProjectName = "P" });

            Assert.False(result.Success);
            Assert.Contains("project", result.Message, StringComparison.OrdinalIgnoreCase);
        }

        // ── AssignUserToProjectAsync: duplicate assignment returns failure ─────

        [Fact]
        public async Task AssignUser_DuplicateAssignment_MessageContainsAlready()
        {
            var user    = MakeUser();
            var project = MakeProject();
            var existing = new ProjectAssignment { Id = 1, UserId = 1, ProjectId = 1 };
            _userRepo.Setup(r => r.GetByIdAsync(1)).ReturnsAsync(user);
            _prjRepo.Setup(r => r.GetByIdAsync(1)).ReturnsAsync(project);
            _asnRepo.Setup(r => r.GetAllAsync()).ReturnsAsync(new List<ProjectAssignment> { existing });

            var result = await CreateService().AssignUserToProjectAsync(
                new ProjectAssignRequest { ProjectId = 1, UserId = 1, ProjectName = "Test Project" });

            Assert.False(result.Success);
            Assert.Contains("already", result.Message, StringComparison.OrdinalIgnoreCase);
        }

        // ── RemoveUserFromProjectAsync: assignment not found returns failure ───

        [Fact]
        public async Task RemoveAssignment_NotFound_MessageContainsNotFound()
        {
            _asnRepo.Setup(r => r.GetByIdAsync(99)).ReturnsAsync((ProjectAssignment?)null);

            var result = await CreateService().RemoveUserFromProjectAsync(99);

            Assert.False(result.Success);
            Assert.Contains("not found", result.Message, StringComparison.OrdinalIgnoreCase);
        }

        // ── GetAllProjectsAsync: pagination works correctly ───────────────────

        [Fact]
        public async Task GetAllProjects_Page2_ReturnsCorrectSlice()
        {
            var projects = Enumerable.Range(1, 9).Select(i => new Project
            {
                Id = i, ProjectName = $"Project {i}", StartDate = DateTime.Today.AddDays(i)
            }).ToList();
            _prjRepo.Setup(r => r.GetAllAsync()).ReturnsAsync(projects);

            var result = await CreateService().GetAllProjectsAsync(pageNumber: 2, pageSize: 4);

            Assert.True(result.Success);
            Assert.Equal(4, result.Data!.Count());
        }

        // ── GetProjectAssignmentsAsync: returns assignments for project ────────

        [Fact]
        public async Task GetProjectAssignments_MultipleAssignments_ReturnsAll()
        {
            var project = MakeProject();
            var user1 = MakeUser(1);
            var user2 = MakeUser(2);
            var assignments = new List<ProjectAssignment>
            {
                new() { Id = 1, ProjectId = 1, UserId = 1 },
                new() { Id = 2, ProjectId = 1, UserId = 2 },
                new() { Id = 3, ProjectId = 2, UserId = 1 }  // different project
            };
            _asnRepo.Setup(r => r.GetAllAsync()).ReturnsAsync(assignments);
            _userRepo.Setup(r => r.GetByIdAsync(1)).ReturnsAsync(user1);
            _userRepo.Setup(r => r.GetByIdAsync(2)).ReturnsAsync(user2);
            _prjRepo.Setup(r => r.GetByIdAsync(1)).ReturnsAsync(project);

            var result = await CreateService().GetProjectAssignmentsAsync(1);

            Assert.True(result.Success);
            Assert.Equal(2, result.Data!.Count());
        }

        // ── GetUserProjectAssignmentsAsync: returns user's assignments ─────────

        [Fact]
        public async Task GetUserAssignments_ReturnsOnlyUserAssignments()
        {
            var user = MakeUser(1, UserRole.Employee);
            var project = MakeProject(1);
            var assignments = new List<ProjectAssignment>
            {
                new() { Id = 1, ProjectId = 1, UserId = 1 },
                new() { Id = 2, ProjectId = 2, UserId = 2 }  // different user
            };
            _userRepo.Setup(r => r.GetByIdAsync(1)).ReturnsAsync(user);
            _asnRepo.Setup(r => r.GetAllAsync()).ReturnsAsync(assignments);
            _prjRepo.Setup(r => r.GetAllAsync()).ReturnsAsync(new List<Project> { project });
            _prjRepo.Setup(r => r.GetByIdAsync(1)).ReturnsAsync(project);

            var result = await CreateService().GetUserProjectAssignmentsAsync(1);

            Assert.True(result.Success);
            Assert.Single(result.Data!);
            Assert.All(result.Data!, a => Assert.Equal(1, a.UserId));
        }
    }

namespace TimeSheetAppWeb.Tests.Services
{
    /// <summary>
    /// Further coverage: assignment verification, pagination, and update flows.
    /// </summary>
    public class ProjectServiceFurtherTests
    {
        private readonly Mock<IRepository<int, Project>>           _prjRepo  = new();
        private readonly Mock<IRepository<int, User>>              _userRepo = new();
        private readonly Mock<IRepository<int, ProjectAssignment>> _asnRepo  = new();
        private readonly Mock<ILogger<ProjectService>>             _logger   = new();

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

        // ── AssignUser — Mentor role not allowed ──────────────────────────────

        [Fact]
        public async Task AssignUser_MentorRole_ReturnsFail()
        {
            var mentor  = MakeUser(1, UserRole.Mentor);
            var project = MakeProject();
            _userRepo.Setup(r => r.GetByIdAsync(1)).ReturnsAsync(mentor);
            _prjRepo.Setup(r => r.GetByIdAsync(1)).ReturnsAsync(project);
            _asnRepo.Setup(r => r.GetAllAsync()).ReturnsAsync(new List<ProjectAssignment>());

            var result = await CreateService().AssignUserToProjectAsync(new ProjectAssignRequest
            {
                ProjectId = 1, UserId = 1, ProjectName = "Test Project"
            });

            Assert.False(result.Success);
        }

        // ── AssignUser — Admin role not allowed ───────────────────────────────

        [Fact]
        public async Task AssignUser_AdminRole_ReturnsFail()
        {
            var admin   = MakeUser(1, UserRole.Admin);
            var project = MakeProject();
            _userRepo.Setup(r => r.GetByIdAsync(1)).ReturnsAsync(admin);
            _prjRepo.Setup(r => r.GetByIdAsync(1)).ReturnsAsync(project);
            _asnRepo.Setup(r => r.GetAllAsync()).ReturnsAsync(new List<ProjectAssignment>());

            var result = await CreateService().AssignUserToProjectAsync(new ProjectAssignRequest
            {
                ProjectId = 1, UserId = 1, ProjectName = "Test Project"
            });

            Assert.False(result.Success);
        }

        // ── AssignUser — Manager role is allowed ──────────────────────────────

        [Fact]
        public async Task AssignUser_ManagerRole_ReturnsSuccess()
        {
            var manager = MakeUser(1, UserRole.Manager);
            var project = MakeProject();
            _userRepo.Setup(r => r.GetByIdAsync(1)).ReturnsAsync(manager);
            _prjRepo.Setup(r => r.GetByIdAsync(1)).ReturnsAsync(project);
            _asnRepo.Setup(r => r.GetAllAsync()).ReturnsAsync(new List<ProjectAssignment>());
            _asnRepo.Setup(r => r.AddAsync(It.IsAny<ProjectAssignment>()))
                    .ReturnsAsync(new ProjectAssignment { Id = 1, UserId = 1, ProjectId = 1 });

            var result = await CreateService().AssignUserToProjectAsync(new ProjectAssignRequest
            {
                ProjectId = 1, UserId = 1, ProjectName = "Test Project"
            });

            Assert.True(result.Success);
        }

        // ── CreateProject — duplicate name ────────────────────────────────────


        // ── GetAllProjects — empty list ───────────────────────────────────────

        [Fact]
        public async Task GetAllProjects_EmptyList_ReturnsEmptyResult()
        {
            _prjRepo.Setup(r => r.GetAllAsync()).ReturnsAsync(new List<Project>());

            var result = await CreateService().GetAllProjectsAsync();

            Assert.True(result.Success);
            Assert.Empty(result.Data!);
        }

        // ── GetProjectAssignments — multiple assignments ───────────────────────

        [Fact]
        public async Task GetProjectAssignments_MultipleAssignments_ReturnsAll()
        {
            var project = MakeProject();
            var user1   = MakeUser(1);
            var user2   = MakeUser(2);
            var asns = new List<ProjectAssignment>
            {
                new() { Id = 1, ProjectId = 1, UserId = 1 },
                new() { Id = 2, ProjectId = 1, UserId = 2 },
                new() { Id = 3, ProjectId = 2, UserId = 1 } // different project
            };
            _asnRepo.Setup(r => r.GetAllAsync()).ReturnsAsync(asns);
            _userRepo.Setup(r => r.GetByIdAsync(1)).ReturnsAsync(user1);
            _userRepo.Setup(r => r.GetByIdAsync(2)).ReturnsAsync(user2);
            _prjRepo.Setup(r => r.GetByIdAsync(1)).ReturnsAsync(project);

            var result = await CreateService().GetProjectAssignmentsAsync(1);

            Assert.True(result.Success);
            Assert.Equal(2, result.Data!.Count());
        }

        // ── GetUserProjectAssignments — pagination ────────────────────────────

        [Fact]
        public async Task GetUserAssignments_Pagination_ReturnsCorrectPage()
        {
            var user = MakeUser(1, UserRole.Employee);
            var projects = Enumerable.Range(1, 6).Select(i => new Project
            {
                Id = i, ProjectName = $"Project {i}", StartDate = DateTime.Today.AddDays(i)
            }).ToList();
            var asns = Enumerable.Range(1, 6).Select(i => new ProjectAssignment
            {
                Id = i, ProjectId = i, UserId = 1
            }).ToList();
            _userRepo.Setup(r => r.GetByIdAsync(1)).ReturnsAsync(user);
            _asnRepo.Setup(r => r.GetAllAsync()).ReturnsAsync(asns);
            _prjRepo.Setup(r => r.GetAllAsync()).ReturnsAsync(projects);
            foreach (var p in projects)
                _prjRepo.Setup(r => r.GetByIdAsync(p.Id)).ReturnsAsync(p);

            var result = await CreateService().GetUserProjectAssignmentsAsync(1, 1, 4);

            Assert.True(result.Success);
            Assert.Equal(4, result.Data!.Count());
        }

        

        // ── DeleteProject — verifies DeleteAsync called ───────────────────────

        [Fact]
        public async Task DeleteProject_Valid_CallsDeleteAsync()
        {
            var project = MakeProject();
            _prjRepo.Setup(r => r.GetByIdAsync(1)).ReturnsAsync(project);
            _prjRepo.Setup(r => r.DeleteAsync(1)).ReturnsAsync(project);

            await CreateService().DeleteProjectAsync(1);

            _prjRepo.Verify(r => r.DeleteAsync(1), Times.Once);
        }

        // ── RemoveAssignment — verifies DeleteAsync called ────────────────────

        [Fact]
        public async Task RemoveAssignment_Valid_CallsDeleteAsync()
        {
            var asn = new ProjectAssignment { Id = 1, ProjectId = 1, UserId = 1 };
            _asnRepo.Setup(r => r.GetByIdAsync(1)).ReturnsAsync(asn);
            _asnRepo.Setup(r => r.DeleteAsync(1)).ReturnsAsync(asn);

            await CreateService().RemoveUserFromProjectAsync(1);

            _asnRepo.Verify(r => r.DeleteAsync(1), Times.Once);
        }
    }
}
