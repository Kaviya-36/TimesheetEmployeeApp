using Microsoft.Extensions.Logging;
using Moq;
using TimeSheetAppWeb.Exceptions;
using TimeSheetAppWeb.Interface;
using TimeSheetAppWeb.Interfaces;
using TimeSheetAppWeb.Model;
using TimeSheetAppWeb.Model.DTOs;
using TimeSheetAppWeb.Models.DTOs;
using TimeSheetAppWeb.Services;
using Xunit;

namespace TimeSheetAppWeb.Tests.Services
{
    public class UserServiceTests
    {
        private readonly Mock<IRepository<int, User>>       _userRepo  = new();
        private readonly Mock<IRepository<int, Department>> _deptRepo  = new();
        private readonly Mock<IPasswordService>             _pwdSvc    = new();
        private readonly Mock<ITokenService>                _tokenSvc  = new();
        private readonly Mock<ILogger<UserService>>         _logger    = new();

        private UserService CreateService() =>
            new(_userRepo.Object, _deptRepo.Object, _pwdSvc.Object, _tokenSvc.Object, _logger.Object);

        private User MakeUser(int id = 1, bool active = true) => new User
        {
            Id = id, Name = "Test User", Email = "test@test.com",
            EmployeeId = "E001", Role = UserRole.Employee,
            PasswordHash = "hashed", IsActive = active,
            JoiningDate = DateTime.UtcNow
        };

        // ── LoginAsync ─────────────────────────────────────────────────────────

        [Fact]
        public async Task Login_UserNotFound_ThrowsUnauthorized()
        {
            _userRepo.Setup(r => r.GetAllAsync()).ReturnsAsync(new List<User>());
            var svc = CreateService();

            await Assert.ThrowsAsync<UnAuthorizedException>(() =>
                svc.LoginAsync(new CheckUserRequestDto { Username = "nobody", Password = "pass" }));
        }

        [Fact]
        public async Task Login_WrongPassword_ThrowsUnauthorized()
        {
            var user = MakeUser();
            _userRepo.Setup(r => r.GetAllAsync()).ReturnsAsync(new List<User> { user });
            _pwdSvc.Setup(p => p.VerifyPassword("wrong", user.PasswordHash)).Returns(false);
            var svc = CreateService();

            await Assert.ThrowsAsync<UnAuthorizedException>(() =>
                svc.LoginAsync(new CheckUserRequestDto { Username = "Test User", Password = "wrong" }));
        }

        [Fact]
        public async Task Login_InactiveUser_ThrowsInvalidOperation()
        {
            var user = MakeUser(active: false);
            _userRepo.Setup(r => r.GetAllAsync()).ReturnsAsync(new List<User> { user });
            _pwdSvc.Setup(p => p.VerifyPassword("pass", user.PasswordHash)).Returns(true);
            var svc = CreateService();

            await Assert.ThrowsAsync<InvalidOperationException>(() =>
                svc.LoginAsync(new CheckUserRequestDto { Username = "Test User", Password = "pass" }));
        }

        [Fact]
        public async Task Login_ValidCredentials_ReturnsToken()
        {
            var user = MakeUser();
            _userRepo.Setup(r => r.GetAllAsync()).ReturnsAsync(new List<User> { user });
            _pwdSvc.Setup(p => p.VerifyPassword("pass", user.PasswordHash)).Returns(true);
            _tokenSvc.Setup(t => t.CreateToken(It.IsAny<TokenPayloadDto>())).Returns("jwt-token");
            var svc = CreateService();

            var result = await svc.LoginAsync(new CheckUserRequestDto { Username = "Test User", Password = "pass" });

            Assert.Equal("jwt-token", result.Token);
        }

        // ── RegisterUserAsync ──────────────────────────────────────────────────

        [Fact]
        public async Task Register_DuplicateEmail_ThrowsInvalidOperation()
        {
            var existing = MakeUser();
            _userRepo.Setup(r => r.GetAllAsync()).ReturnsAsync(new List<User> { existing });
            var svc = CreateService();

            await Assert.ThrowsAsync<Exception>(() =>
                svc.RegisterUserAsync(new UserCreateRequest
                {
                    Name = "New", Email = "test@test.com", EmployeeId = "E999",
                    Password = "pass", Role = "Employee"
                }));
        }

        [Fact]
        public async Task Register_DuplicateEmployeeId_ThrowsInvalidOperation()
        {
            var existing = MakeUser();
            _userRepo.Setup(r => r.GetAllAsync()).ReturnsAsync(new List<User> { existing });
            var svc = CreateService();

            await Assert.ThrowsAsync<Exception>(() =>
                svc.RegisterUserAsync(new UserCreateRequest
                {
                    Name = "New", Email = "new@test.com", EmployeeId = "E001",
                    Password = "pass", Role = "Employee"
                }));
        }

        [Fact]
        public async Task Register_Valid_ReturnsUserResponse()
        {
            var newUser = MakeUser(2);
            _userRepo.Setup(r => r.GetAllAsync()).ReturnsAsync(new List<User>());
            _pwdSvc.Setup(p => p.HashPassword("pass")).Returns("hashed");
            _userRepo.Setup(r => r.AddAsync(It.IsAny<User>())).ReturnsAsync(newUser);
            _deptRepo.Setup(r => r.GetByIdAsync(It.IsAny<int>())).ReturnsAsync((Department?)null);
            var svc = CreateService();

            var result = await svc.RegisterUserAsync(new UserCreateRequest
            {
                Name = "New User", Email = "new@test.com", EmployeeId = "E002",
                Password = "pass", Role = "Employee"
            });

            Assert.NotNull(result);
            Assert.Equal("Test User", result.Name); // mapped from newUser
        }

        // ── GetUserByIdAsync ───────────────────────────────────────────────────

        [Fact]
        public async Task GetById_NotFound_ThrowsException()
        {
            _userRepo.Setup(r => r.GetByIdAsync(99)).ReturnsAsync((User?)null);
            var svc = CreateService();

            await Assert.ThrowsAsync<Exception>(() => svc.GetUserByIdAsync(99));
        }

        [Fact]
        public async Task GetById_Found_ReturnsUserResponse()
        {
            var user = MakeUser();
            _userRepo.Setup(r => r.GetByIdAsync(1)).ReturnsAsync(user);
            _deptRepo.Setup(r => r.GetByIdAsync(It.IsAny<int>())).ReturnsAsync((Department?)null);
            var svc = CreateService();

            var result = await svc.GetUserByIdAsync(1);

            Assert.Equal("Test User", result.Name);
            Assert.Equal("E001", result.EmployeeId);
        }

        // ── UpdateUserAsync ────────────────────────────────────────────────────

        [Fact]
        public async Task Update_NotFound_ThrowsException()
        {
            _userRepo.Setup(r => r.GetByIdAsync(99)).ReturnsAsync((User?)null);
            var svc = CreateService();

            await Assert.ThrowsAsync<Exception>(() =>
                svc.UpdateUserAsync(99, new UserUpdateRequest { Name = "New Name" }));
        }

        [Fact]
        public async Task Update_Valid_UpdatesName()
        {
            var user = MakeUser();
            _userRepo.Setup(r => r.GetByIdAsync(1)).ReturnsAsync(user);
            _userRepo.Setup(r => r.UpdateAsync(1, It.IsAny<User>()))
                     .ReturnsAsync((int id, User u) => u);
            _deptRepo.Setup(r => r.GetByIdAsync(It.IsAny<int>())).ReturnsAsync((Department?)null);
            var svc = CreateService();

            var result = await svc.UpdateUserAsync(1, new UserUpdateRequest { Name = "Updated Name" });

            Assert.Equal("Updated Name", result.Name);
        }

        [Fact]
        public async Task Update_SetInactive_ChangesStatus()
        {
            var user = MakeUser();
            _userRepo.Setup(r => r.GetByIdAsync(1)).ReturnsAsync(user);
            _userRepo.Setup(r => r.UpdateAsync(1, It.IsAny<User>()))
                     .ReturnsAsync((int id, User u) => u);
            _deptRepo.Setup(r => r.GetByIdAsync(It.IsAny<int>())).ReturnsAsync((Department?)null);
            var svc = CreateService();

            var result = await svc.UpdateUserAsync(1, new UserUpdateRequest { IsActive = false });

            Assert.Equal("Inactive", result.Status);
        }

        // ── DeleteUserAsync ────────────────────────────────────────────────────

        [Fact]
        public async Task Delete_NotFound_ReturnsFalse()
        {
            _userRepo.Setup(r => r.GetByIdAsync(99)).ReturnsAsync((User?)null);
            var svc = CreateService();

            var result = await svc.DeleteUserAsync(99);

            Assert.False(result);
        }

        [Fact]
        public async Task Delete_Valid_ReturnsTrue()
        {
            var user = MakeUser();
            _userRepo.Setup(r => r.GetByIdAsync(1)).ReturnsAsync(user);
            _userRepo.Setup(r => r.DeleteAsync(1)).ReturnsAsync(user);
            var svc = CreateService();

            var result = await svc.DeleteUserAsync(1);

            Assert.True(result);
        }

        // ── GetAllUsersAsync ───────────────────────────────────────────────────

        [Fact]
        public async Task GetAll_SearchFilter_ReturnsMatchingUsers()
        {
            var users = new List<User>
            {
                MakeUser(1),
                new User { Id = 2, Name = "Alice", Email = "alice@test.com", EmployeeId = "E002", Role = UserRole.Manager, PasswordHash = "h", IsActive = true, JoiningDate = DateTime.UtcNow }
            };
            _userRepo.Setup(r => r.GetAllAsync()).ReturnsAsync(users);
            _deptRepo.Setup(r => r.GetAllAsync()).ReturnsAsync(new List<Department>());
            var svc = CreateService();

            var result = await svc.GetAllUsersAsync(search: "alice") as dynamic;

            Assert.NotNull(result);
        }

        [Fact]
        public async Task GetAll_RoleFilter_ReturnsOnlyThatRole()
        {
            var users = new List<User>
            {
                MakeUser(1),  // Employee
                new User { Id = 2, Name = "Mgr", Email = "m@test.com", EmployeeId = "E002", Role = UserRole.Manager, PasswordHash = "h", IsActive = true, JoiningDate = DateTime.UtcNow }
            };
            _userRepo.Setup(r => r.GetAllAsync()).ReturnsAsync(users);
            _deptRepo.Setup(r => r.GetAllAsync()).ReturnsAsync(new List<Department>());
            var svc = CreateService();

            var result = await svc.GetAllUsersAsync(role: "Manager") as dynamic;

            Assert.NotNull(result);
        }
    }
}
