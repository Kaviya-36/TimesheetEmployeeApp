using Xunit;
using Moq;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using TimeSheetAppWeb.Interface;
using TimeSheetAppWeb.Interfaces;
using TimeSheetAppWeb.Model;
using TimeSheetAppWeb.Model.DTOs;
using TimeSheetAppWeb.Models.DTOs;
using TimeSheetAppWeb.Services;
using TimeSheetAppWeb.Exceptions;

public class UserServiceTests
{
    private readonly Mock<IRepository<int, User>> _userRepoMock;
    private readonly Mock<IPasswordService> _passwordServiceMock;
    private readonly Mock<ITokenService> _tokenServiceMock;
    private readonly UserService _service;

    public UserServiceTests()
    {
        _userRepoMock = new Mock<IRepository<int, User>>();
        _passwordServiceMock = new Mock<IPasswordService>();
        _tokenServiceMock = new Mock<ITokenService>();

        _service = new UserService(
            _userRepoMock.Object,
            _passwordServiceMock.Object,
            _tokenServiceMock.Object);
    }

    // ================= LOGIN =================

    [Fact]
    public async Task LoginAsync_Should_Return_Token_When_Valid()
    {
        var user = new User
        {
            Id = 1,
            Name = "john",
            PasswordHash = "hashed",
            Role = UserRole.Admin
        };

        _userRepoMock.Setup(r => r.GetAllAsync())
                     .ReturnsAsync(new List<User> { user });

        _passwordServiceMock.Setup(p => p.VerifyPassword("password", "hashed"))
                            .Returns(true);

        _tokenServiceMock.Setup(t => t.CreateToken(It.IsAny<TokenPayloadDto>()))
                         .Returns("jwt-token");

        var request = new CheckUserRequestDto
        {
            Username = "john",
            Password = "password"
        };

        var result = await _service.LoginAsync(request);

        Assert.Equal("jwt-token", result.Token);
        Assert.Equal(1, result.UserId);
        Assert.Equal("Admin", result.Role);
    }

    [Fact]
    public async Task LoginAsync_Should_Throw_When_Invalid()
    {
        _userRepoMock.Setup(r => r.GetAllAsync())
                     .ReturnsAsync(new List<User>());

        var request = new CheckUserRequestDto
        {
            Username = "wrong",
            Password = "wrong"
        };

        await Assert.ThrowsAsync<Exception>(() => _service.LoginAsync(request));
    }

    // ================= REGISTER =================

    [Fact]
    public async Task RegisterUserAsync_Should_Succeed()
    {
        _userRepoMock.Setup(r => r.GetAllAsync())
                     .ReturnsAsync(new List<User>());

        _passwordServiceMock.Setup(p => p.HashPassword("123"))
                            .Returns("hashed123");

        _userRepoMock.Setup(r => r.AddAsync(It.IsAny<User>()))
                     .ReturnsAsync((User u) => u);

        var request = new UserCreateRequest
        {
            EmployeeId = "EMP001",
            Name = "John",
            Email = "john@test.com",
            Phone = "123456",
            DepartmentId = 1,
            Role = "Admin",
            Password = "123"
        };

        var result = await _service.RegisterUserAsync(request);

        Assert.Equal("John", result.Name);
        Assert.Equal("Admin", result.Role);
        Assert.Equal("Active", result.Status);
    }

    [Fact]
    public async Task RegisterUserAsync_Should_Throw_When_Email_Exists()
    {
        var existingUser = new User
        {
            Email = "john@test.com",
            EmployeeId = "EMP001"
        };

        _userRepoMock.Setup(r => r.GetAllAsync())
                     .ReturnsAsync(new List<User> { existingUser });

        var request = new UserCreateRequest
        {
            Email = "john@test.com",
            EmployeeId = "EMP002",
            Password = "123"
        };

        await Assert.ThrowsAsync<Exception>(() =>
            _service.RegisterUserAsync(request));
    }

    // ================= GET USER =================

    [Fact]
    public async Task GetUserByIdAsync_Should_Return_User()
    {
        var user = new User
        {
            Id = 1,
            Name = "John",
            EmployeeId = "EMP001",
            Role = UserRole.Employee,
            IsActive = true
        };

        _userRepoMock.Setup(r => r.GetByIdAsync(1))
                     .ReturnsAsync(user);

        var result = await _service.GetUserByIdAsync(1);

        Assert.Equal("John", result.Name);
        Assert.Equal("Active", result.Status);
    }

    // ================= UPDATE =================

    [Fact]
    public async Task UpdateUserAsync_Should_Succeed()
    {
        var user = new User
        {
            Id = 1,
            Name = "Old",
            Role = UserRole.Employee,
            IsActive = true
        };

        _userRepoMock.Setup(r => r.GetByIdAsync(1))
                     .ReturnsAsync(user);

        _userRepoMock.Setup(r => r.UpdateAsync(1, user))
                     .ReturnsAsync(user);

        var request = new UserUpdateRequest
        {
            Name = "New",
            Role = "Admin"
        };

        var result = await _service.UpdateUserAsync(1, request);

        Assert.Equal("New", result.Name);
        Assert.Equal("Admin", result.Role);
    }

    // ================= DELETE =================

    [Fact]
    public async Task DeleteUserAsync_Should_Return_True_When_Exists()
    {
        var user = new User { Id = 1 };

        _userRepoMock.Setup(r => r.GetByIdAsync(1))
                     .ReturnsAsync(user);

        _userRepoMock.Setup(r => r.DeleteAsync(1))
                     .ReturnsAsync(user);

        var result = await _service.DeleteUserAsync(1);

        Assert.True(result);
    }

    [Fact]
    public async Task DeleteUserAsync_Should_Return_False_When_Not_Found()
    {
        _userRepoMock.Setup(r => r.GetByIdAsync(1))
                     .ReturnsAsync((User)null);

        var result = await _service.DeleteUserAsync(1);

        Assert.False(result);
    }
}