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

public class ProjectServiceTests
{
    private readonly Mock<IRepository<int, Project>> _projectRepoMock;
    private readonly Mock<IRepository<int, User>> _userRepoMock;
    private readonly Mock<IRepository<int, ProjectAssignment>> _assignmentRepoMock;
    private readonly ProjectService _service;

    public ProjectServiceTests()
    {
        _projectRepoMock = new Mock<IRepository<int, Project>>();
        _userRepoMock = new Mock<IRepository<int, User>>();
        _assignmentRepoMock = new Mock<IRepository<int, ProjectAssignment>>();

        _service = new ProjectService(
            _projectRepoMock.Object,
            _userRepoMock.Object,
            _assignmentRepoMock.Object);
    }

    // ================= CREATE PROJECT =================

    [Fact]
    public async Task CreateProjectAsync_Should_Succeed()
    {
        var manager = new User { Id = 1, Name = "Manager1" };

        _userRepoMock.Setup(r => r.GetByIdAsync(1))
                     .ReturnsAsync(manager);

        _projectRepoMock.Setup(r => r.AddAsync(It.IsAny<Project>()))
                        .ReturnsAsync((Project p) => p);

        var request = new ProjectCreateRequest
        {
            ProjectName = "Test Project",
            Description = "Desc",
            ManagerId = 1
        };

        var result = await _service.CreateProjectAsync(request);

        Assert.True(result.Success);
        Assert.Equal("Project created successfully", result.Message);
        Assert.Equal("Manager1", result.Data!.ManagerName);
    }

    [Fact]
    public async Task CreateProjectAsync_Should_Fail_When_Manager_Not_Found()
    {
        _userRepoMock.Setup(r => r.GetByIdAsync(1))
                     .ReturnsAsync((User)null);

        var request = new ProjectCreateRequest
        {
            ProjectName = "Test",
            ManagerId = 1
        };

        var result = await _service.CreateProjectAsync(request);

        Assert.False(result.Success);
        Assert.Equal("Manager not found", result.Message);
    }

    // ================= UPDATE PROJECT =================

    [Fact]
    public async Task UpdateProjectAsync_Should_Succeed()
    {
        var project = new Project
        {
            Id = 1,
            ProjectName = "Old",
            Description = "OldDesc"
        };

        _projectRepoMock.Setup(r => r.GetByIdAsync(1))
                        .ReturnsAsync(project);

        _projectRepoMock.Setup(r => r.UpdateAsync(1, project))
                        .ReturnsAsync(project);

        var request = new ProjectUpdateRequest
        {
            ProjectName = "New"
        };

        var result = await _service.UpdateProjectAsync(1, request);

        Assert.True(result.Success);
        Assert.Equal("Project updated successfully", result.Message);
        Assert.Equal("New", result.Data!.ProjectName);
    }

    [Fact]
    public async Task UpdateProjectAsync_Should_Fail_When_Not_Found()
    {
        _projectRepoMock.Setup(r => r.GetByIdAsync(1))
                        .ReturnsAsync((Project)null);

        var result = await _service.UpdateProjectAsync(1, new ProjectUpdateRequest());

        Assert.False(result.Success);
        Assert.Equal("Project not found", result.Message);
    }

    // ================= DELETE PROJECT =================

    [Fact]
    public async Task DeleteProjectAsync_Should_Succeed()
    {
        var project = new Project { Id = 1 };

        _projectRepoMock.Setup(r => r.GetByIdAsync(1))
                        .ReturnsAsync(project);

        _projectRepoMock.Setup(r => r.DeleteAsync(1))
                        .ReturnsAsync(project);

        var result = await _service.DeleteProjectAsync(1);

        Assert.True(result.Success);
        Assert.True(result.Data);
    }

    [Fact]
    public async Task DeleteProjectAsync_Should_Fail_When_Not_Found()
    {
        _projectRepoMock.Setup(r => r.GetByIdAsync(1))
                        .ReturnsAsync((Project)null);

        var result = await _service.DeleteProjectAsync(1);

        Assert.False(result.Success);
        Assert.False(result.Data);
    }

    // ================= ASSIGN USER =================

    [Fact]
    public async Task AssignUserToProjectAsync_Should_Succeed()
    {
        var project = new Project { Id = 1, ProjectName = "Test" };
        var user = new User { Id = 2, Name = "User1", Email = "user@test.com" };

        _projectRepoMock.Setup(r => r.GetByIdAsync(1))
                        .ReturnsAsync(project);

        _userRepoMock.Setup(r => r.GetByIdAsync(2))
                     .ReturnsAsync(user);

        _assignmentRepoMock.Setup(r => r.AddAsync(It.IsAny<ProjectAssignment>()))
                           .ReturnsAsync((ProjectAssignment a) => a);

        var request = new ProjectAssignRequest
        {
            ProjectId = 1,
            UserId = 2
        };

        var result = await _service.AssignUserToProjectAsync(request);

        Assert.True(result.Success);
        Assert.Equal("User assigned successfully", result.Message);
        Assert.Equal("User1", result.Data!.UserName);
    }

    // ================= REMOVE USER =================

    [Fact]
    public async Task RemoveUserFromProjectAsync_Should_Succeed()
    {
        var assignment = new ProjectAssignment { Id = 1 };

        _assignmentRepoMock.Setup(r => r.GetByIdAsync(1))
                           .ReturnsAsync(assignment);

        _assignmentRepoMock.Setup(r => r.DeleteAsync(1))
                           .ReturnsAsync(assignment);

        var result = await _service.RemoveUserFromProjectAsync(1);

        Assert.True(result.Success);
        Assert.True(result.Data);
    }

    // ================= GET PROJECT ASSIGNMENTS =================

    [Fact]
    public async Task GetProjectAssignmentsAsync_Should_Return_List()
    {
        var assignment = new ProjectAssignment
        {
            Id = 1,
            ProjectId = 1,
            UserId = 2
        };

        var project = new Project { Id = 1, ProjectName = "Test" };
        var user = new User { Id = 2, Name = "User1", Email = "user@test.com" };

        _assignmentRepoMock.Setup(r => r.GetAllAsync())
                           .ReturnsAsync(new List<ProjectAssignment> { assignment });

        _projectRepoMock.Setup(r => r.GetByIdAsync(1))
                        .ReturnsAsync(project);

        _userRepoMock.Setup(r => r.GetByIdAsync(2))
                     .ReturnsAsync(user);

        var result = await _service.GetProjectAssignmentsAsync(1);

        Assert.True(result.Success);
        Assert.Single(result.Data!);
    }
}