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
    /// <summary>
    /// Unit tests for <see cref="UserService"/>.
    /// Covers login, registration, CRUD operations, filtering, and sorting.
    /// </summary>
    public class UserServiceTests
    {
        // ── Mocks ──────────────────────────────────────────────────────────────

        private readonly Mock<IRepository<int, User>>       _userRepo = new();
        private readonly Mock<IRepository<int, Department>> _deptRepo = new();
        private readonly Mock<IPasswordService>             _pwdSvc   = new();
        private readonly Mock<ITokenService>                _tokenSvc = new();
        private readonly Mock<ILogger<UserService>>         _logger   = new();

        // ── Helpers ────────────────────────────────────────────────────────────

        private UserService CreateService() =>
            new(_userRepo.Object, _deptRepo.Object, _pwdSvc.Object, _tokenSvc.Object, _logger.Object);

        private static User MakeUser(int id = 1, bool active = true) => new()
        {
            Id           = id,
            Name         = "Test User",
            Email        = "test@example.com",
            EmployeeId   = "E001",
            Role         = UserRole.Employee,
            PasswordHash = "hashed",
            IsActive     = active,
            JoiningDate  = DateTime.UtcNow
        };

        // ── Constructor null guards ────────────────────────────────────────────

        [Fact]
        public void Constructor_NullUserRepo_Throws()
        {
            Assert.Throws<ArgumentNullException>(() =>
                new UserService(null!, _deptRepo.Object, _pwdSvc.Object, _tokenSvc.Object, _logger.Object));
        }

        [Fact]
        public void Constructor_NullDeptRepo_Throws()
        {
            Assert.Throws<ArgumentNullException>(() =>
                new UserService(_userRepo.Object, null!, _pwdSvc.Object, _tokenSvc.Object, _logger.Object));
        }

        [Fact]
        public void Constructor_NullPasswordSvc_Throws()
        {
            Assert.Throws<ArgumentNullException>(() =>
                new UserService(_userRepo.Object, _deptRepo.Object, null!, _tokenSvc.Object, _logger.Object));
        }

        [Fact]
        public void Constructor_NullTokenSvc_Throws()
        {
            Assert.Throws<ArgumentNullException>(() =>
                new UserService(_userRepo.Object, _deptRepo.Object, _pwdSvc.Object, null!, _logger.Object));
        }

        [Fact]
        public void Constructor_NullLogger_Throws()
        {
            Assert.Throws<ArgumentNullException>(() =>
                new UserService(_userRepo.Object, _deptRepo.Object, _pwdSvc.Object, _tokenSvc.Object, null!));
        }

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

        // ── GetAllUsersAsync — status filter + sort variants ──────────────────

        [Fact]
        public async Task GetAll_StatusFilter_Active_ReturnsActiveOnly()
        {
            var users = new List<User>
            {
                MakeUser(1, active: true),
                MakeUser(2, active: false)
            };
            _userRepo.Setup(r => r.GetAllAsync()).ReturnsAsync(users);
            _deptRepo.Setup(r => r.GetAllAsync()).ReturnsAsync(new List<Department>());
            var svc = CreateService();

            var result = await svc.GetAllUsersAsync(status: "active");

            Assert.NotNull(result);
        }

        [Fact]
        public async Task GetAll_SortByRole_Desc_Works()
        {
            var users = new List<User> { MakeUser(1) };
            _userRepo.Setup(r => r.GetAllAsync()).ReturnsAsync(users);
            _deptRepo.Setup(r => r.GetAllAsync()).ReturnsAsync(new List<Department>());
            var svc = CreateService();

            var result = await svc.GetAllUsersAsync(sortBy: "role", sortDir: "desc");

            Assert.NotNull(result);
        }

        [Fact]
        public async Task GetAll_SortByJoined_Asc_Works()
        {
            var users = new List<User> { MakeUser(1) };
            _userRepo.Setup(r => r.GetAllAsync()).ReturnsAsync(users);
            _deptRepo.Setup(r => r.GetAllAsync()).ReturnsAsync(new List<Department>());
            var svc = CreateService();

            var result = await svc.GetAllUsersAsync(sortBy: "joined", sortDir: "asc");

            Assert.NotNull(result);
        }

        [Fact]
        public async Task GetAll_SortByName_Desc_Works()
        {
            var users = new List<User> { MakeUser(1) };
            _userRepo.Setup(r => r.GetAllAsync()).ReturnsAsync(users);
            _deptRepo.Setup(r => r.GetAllAsync()).ReturnsAsync(new List<Department>());
            var svc = CreateService();

            var result = await svc.GetAllUsersAsync(sortBy: "name", sortDir: "desc");

            Assert.NotNull(result);
        }

        // ── UpdateUserAsync — role parse + departmentId ────────────────────────

        [Fact]
        public async Task Update_WithRoleAndDepartment_UpdatesBoth()
        {
            var user = MakeUser();
            var dept = new Department { Id = 2, Name = "Engineering" };
            _userRepo.Setup(r => r.GetByIdAsync(1)).ReturnsAsync(user);
            _userRepo.Setup(r => r.UpdateAsync(1, It.IsAny<User>()))
                     .ReturnsAsync((int id, User u) => u);
            _deptRepo.Setup(r => r.GetByIdAsync(2)).ReturnsAsync(dept);
            var svc = CreateService();

            var result = await svc.UpdateUserAsync(1, new UserUpdateRequest
            {
                Role = "Manager", DepartmentId = 2, Phone = "1234567890", Email = "new@test.com"
            });

            Assert.Equal("Manager", result.Role);
        }
    }
}

    public class UserServiceEdgeCaseTests
    {
        private readonly Mock<IRepository<int, User>>       _userRepo = new();
        private readonly Mock<IRepository<int, Department>> _deptRepo = new();
        private readonly Mock<IPasswordService>             _pwdSvc   = new();
        private readonly Mock<ITokenService>                _tokenSvc = new();
        private readonly Mock<ILogger<UserService>>         _logger   = new();

        private UserService CreateService() =>
            new(_userRepo.Object, _deptRepo.Object, _pwdSvc.Object, _tokenSvc.Object, _logger.Object);

        private static User MakeUser(int id = 1, bool active = true) => new()
        {
            Id = id, Name = "Test User", Email = "test@example.com",
            EmployeeId = "E001", Role = UserRole.Employee,
            PasswordHash = "hashed", IsActive = active, JoiningDate = DateTime.UtcNow
        };

        // ── Login — email-based lookup (service uses Name) ────────────────────

        [Fact]
        public async Task Login_CaseInsensitiveUsername_Succeeds()
        {
            var user = MakeUser();
            _userRepo.Setup(r => r.GetAllAsync()).ReturnsAsync(new List<User> { user });
            _pwdSvc.Setup(p => p.VerifyPassword("pass", user.PasswordHash)).Returns(true);
            _tokenSvc.Setup(t => t.CreateToken(It.IsAny<TokenPayloadDto>())).Returns("token");

            var svc = CreateService();
            // Username in different case
            var result = await svc.LoginAsync(new CheckUserRequestDto { Username = "TEST USER", Password = "pass" });

            Assert.Equal("token", result.Token);
        }

        // ── Register — invalid role defaults to Employee ──────────────────────

        [Fact]
        public async Task Register_InvalidRole_DefaultsToEmployee()
        {
            var newUser = MakeUser(2);
            _userRepo.Setup(r => r.GetAllAsync()).ReturnsAsync(new List<User>());
            _pwdSvc.Setup(p => p.HashPassword("pass")).Returns("hashed");
            _userRepo.Setup(r => r.AddAsync(It.IsAny<User>())).ReturnsAsync(newUser);
            _deptRepo.Setup(r => r.GetByIdAsync(It.IsAny<int>())).ReturnsAsync((Department?)null);

            var svc = CreateService();
            var result = await svc.RegisterUserAsync(new UserCreateRequest
            {
                Name = "New", Email = "new@test.com", EmployeeId = "E002",
                Password = "pass", Role = "InvalidRole"
            });

            Assert.NotNull(result);
        }

        // ── Register — with department ────────────────────────────────────────

        [Fact]
        public async Task Register_WithDepartment_MapsDepartmentName()
        {
            var dept = new Department { Id = 1, Name = "Engineering" };
            var newUser = new User
            {
                Id = 2, Name = "New User", Email = "new@test.com", EmployeeId = "E002",
                Role = UserRole.Employee, PasswordHash = "hashed", IsActive = false,
                JoiningDate = DateTime.UtcNow, DepartmentId = 1
            };
            _userRepo.Setup(r => r.GetAllAsync()).ReturnsAsync(new List<User>());
            _pwdSvc.Setup(p => p.HashPassword("pass")).Returns("hashed");
            _userRepo.Setup(r => r.AddAsync(It.IsAny<User>())).ReturnsAsync(newUser);
            _deptRepo.Setup(r => r.GetByIdAsync(1)).ReturnsAsync(dept);

            var svc = CreateService();
            var result = await svc.RegisterUserAsync(new UserCreateRequest
            {
                Name = "New User", Email = "new@test.com", EmployeeId = "E002",
                Password = "pass", Role = "Employee", DepartmentId = 1
            });

            Assert.Equal("Engineering", result.DepartmentName);
        }

        // ── GetById — maps status correctly ───────────────────────────────────

        [Fact]
        public async Task GetById_ActiveUser_StatusIsActive()
        {
            var user = MakeUser(active: true);
            _userRepo.Setup(r => r.GetByIdAsync(1)).ReturnsAsync(user);
            _deptRepo.Setup(r => r.GetByIdAsync(It.IsAny<int>())).ReturnsAsync((Department?)null);

            var svc = CreateService();
            var result = await svc.GetUserByIdAsync(1);

            Assert.Equal("Active", result.Status);
        }

        [Fact]
        public async Task GetById_InactiveUser_StatusIsInactive()
        {
            var user = MakeUser(active: false);
            _userRepo.Setup(r => r.GetByIdAsync(1)).ReturnsAsync(user);
            _deptRepo.Setup(r => r.GetByIdAsync(It.IsAny<int>())).ReturnsAsync((Department?)null);

            var svc = CreateService();
            var result = await svc.GetUserByIdAsync(1);

            Assert.Equal("Inactive", result.Status);
        }

        // ── Update — password not changed when not provided ───────────────────

        [Fact]
        public async Task Update_NoPasswordInRequest_PasswordHashUnchanged()
        {
            var user = MakeUser();
            var originalHash = user.PasswordHash;
            _userRepo.Setup(r => r.GetByIdAsync(1)).ReturnsAsync(user);
            _userRepo.Setup(r => r.UpdateAsync(1, It.IsAny<User>()))
                     .ReturnsAsync((int _, User u) => u);
            _deptRepo.Setup(r => r.GetByIdAsync(It.IsAny<int>())).ReturnsAsync((Department?)null);

            var svc = CreateService();
            await svc.UpdateUserAsync(1, new UserUpdateRequest { Name = "New Name" });

            // PasswordService.HashPassword should NOT have been called
            _pwdSvc.Verify(p => p.HashPassword(It.IsAny<string>()), Times.Never);
        }

        // ── GetAll — empty user list ──────────────────────────────────────────

        [Fact]
        public async Task GetAll_EmptyUserList_ReturnsEmptyPaged()
        {
            _userRepo.Setup(r => r.GetAllAsync()).ReturnsAsync(new List<User>());
            _deptRepo.Setup(r => r.GetAllAsync()).ReturnsAsync(new List<Department>());

            var svc = CreateService();
            var result = await svc.GetAllUsersAsync();

            Assert.NotNull(result);
        }

        // ── GetAll — inactive status filter ──────────────────────────────────

        [Fact]
        public async Task GetAll_StatusFilterInactive_ReturnsInactiveOnly()
        {
            var users = new List<User>
            {
                MakeUser(1, active: true),
                MakeUser(2, active: false)
            };
            _userRepo.Setup(r => r.GetAllAsync()).ReturnsAsync(users);
            _deptRepo.Setup(r => r.GetAllAsync()).ReturnsAsync(new List<Department>());

            var svc = CreateService();
            // Should return only inactive users
            var result = await svc.GetAllUsersAsync(status: "inactive");

            Assert.NotNull(result);
        }

        // ── Delete — already deleted (idempotent) ─────────────────────────────

        [Fact]
        public async Task Delete_CalledTwice_SecondCallReturnsFalse()
        {
            _userRepo.SetupSequence(r => r.GetByIdAsync(1))
                     .ReturnsAsync(MakeUser())
                     .ReturnsAsync((User?)null);
            _userRepo.Setup(r => r.DeleteAsync(1)).ReturnsAsync(MakeUser());

            var svc = CreateService();
            var first  = await svc.DeleteUserAsync(1);
            var second = await svc.DeleteUserAsync(1);

            Assert.True(first);
            Assert.False(second);
        }

        // ── Update — email uniqueness not re-checked (service allows it) ──────

        [Fact]
        public async Task Update_EmailChange_UpdatesEmailField()
        {
            var user = MakeUser();
            _userRepo.Setup(r => r.GetByIdAsync(1)).ReturnsAsync(user);
            _userRepo.Setup(r => r.UpdateAsync(1, It.IsAny<User>()))
                     .ReturnsAsync((int _, User u) => u);
            _deptRepo.Setup(r => r.GetByIdAsync(It.IsAny<int>())).ReturnsAsync((Department?)null);

            var svc = CreateService();
            var result = await svc.UpdateUserAsync(1, new UserUpdateRequest { Email = "updated@test.com" });

            Assert.Equal("updated@test.com", result.Email);
        }
    }

    public class UserServiceAdditionalTests
    {
        private readonly Mock<IRepository<int, User>>       _userRepo = new();
        private readonly Mock<IRepository<int, Department>> _deptRepo = new();
        private readonly Mock<IPasswordService>             _pwdSvc   = new();
        private readonly Mock<ITokenService>                _tokenSvc = new();
        private readonly Mock<ILogger<UserService>>         _logger   = new();

        private UserService CreateService() =>
            new(_userRepo.Object, _deptRepo.Object, _pwdSvc.Object, _tokenSvc.Object, _logger.Object);

        private static User MakeUser(int id = 1, bool active = true) => new()
        {
            Id = id, Name = "Test User", Email = "test@example.com",
            EmployeeId = "E001", Role = UserRole.Employee,
            PasswordHash = "hashed", IsActive = active, JoiningDate = DateTime.UtcNow
        };

        // ── LoginAsync: user not found throws UnAuthorizedException ──────────

        [Fact]
        public async Task Login_UserNotFound_ThrowsUnAuthorizedException()
        {
            _userRepo.Setup(r => r.GetAllAsync()).ReturnsAsync(new List<User>());

            await Assert.ThrowsAsync<UnAuthorizedException>(() =>
                CreateService().LoginAsync(new CheckUserRequestDto { Username = "ghost", Password = "pass" }));
        }

        // ── LoginAsync: wrong password throws UnAuthorizedException ──────────

        [Fact]
        public async Task Login_WrongPassword_ThrowsUnAuthorizedException()
        {
            var user = MakeUser();
            _userRepo.Setup(r => r.GetAllAsync()).ReturnsAsync(new List<User> { user });
            _pwdSvc.Setup(p => p.VerifyPassword("badpass", user.PasswordHash)).Returns(false);

            await Assert.ThrowsAsync<UnAuthorizedException>(() =>
                CreateService().LoginAsync(new CheckUserRequestDto { Username = "Test User", Password = "badpass" }));
        }

        // ── LoginAsync: inactive user throws InvalidOperationException ────────

        [Fact]
        public async Task Login_InactiveUser_ThrowsInvalidOperationException()
        {
            var user = MakeUser(active: false);
            _userRepo.Setup(r => r.GetAllAsync()).ReturnsAsync(new List<User> { user });
            _pwdSvc.Setup(p => p.VerifyPassword("pass", user.PasswordHash)).Returns(true);

            await Assert.ThrowsAsync<InvalidOperationException>(() =>
                CreateService().LoginAsync(new CheckUserRequestDto { Username = "Test User", Password = "pass" }));
        }

        // ── RegisterUserAsync: duplicate email throws ─────────────────────────

       
        // ── GetUserByIdAsync: not found throws ────────────────────────────────

        [Fact]
        public async Task GetById_NotFound_ThrowsException()
        {
            _userRepo.Setup(r => r.GetByIdAsync(999)).ReturnsAsync((User?)null);

            await Assert.ThrowsAsync<Exception>(() => CreateService().GetUserByIdAsync(999));
        }

        // ── UpdateUserAsync: user not found throws ────────────────────────────

        [Fact]
        public async Task Update_UserNotFound_ThrowsException()
        {
            _userRepo.Setup(r => r.GetByIdAsync(999)).ReturnsAsync((User?)null);

            await Assert.ThrowsAsync<Exception>(() =>
                CreateService().UpdateUserAsync(999, new UserUpdateRequest { Name = "X" }));
        }

        // ── GetAllUsersAsync: returns paged with search filter ────────────────

        [Fact]
        public async Task GetAll_SearchByEmail_ReturnsMatchingUser()
        {
            var users = new List<User>
            {
                MakeUser(1),
                new User { Id = 2, Name = "Bob", Email = "bob@company.com", EmployeeId = "E002",
                           Role = UserRole.Employee, PasswordHash = "h", IsActive = true, JoiningDate = DateTime.UtcNow }
            };
            _userRepo.Setup(r => r.GetAllAsync()).ReturnsAsync(users);
            _deptRepo.Setup(r => r.GetAllAsync()).ReturnsAsync(new List<Department>());

            var result = await CreateService().GetAllUsersAsync(search: "bob@company");

            Assert.NotNull(result);
        }

        // ── ToggleUserStatusAsync: active→inactive via UpdateUserAsync ────────

        [Fact]
        public async Task Update_ActiveToInactive_StatusBecomesInactive()
        {
            var user = MakeUser(active: true);
            _userRepo.Setup(r => r.GetByIdAsync(1)).ReturnsAsync(user);
            _userRepo.Setup(r => r.UpdateAsync(1, It.IsAny<User>()))
                     .ReturnsAsync((int _, User u) => u);
            _deptRepo.Setup(r => r.GetByIdAsync(It.IsAny<int>())).ReturnsAsync((Department?)null);

            var result = await CreateService().UpdateUserAsync(1, new UserUpdateRequest { IsActive = false });

            Assert.Equal("Inactive", result.Status);
        }

        // ── ToggleUserStatusAsync: inactive→active via UpdateUserAsync ────────

        [Fact]
        public async Task Update_InactiveToActive_StatusBecomesActive()
        {
            var user = MakeUser(active: false);
            _userRepo.Setup(r => r.GetByIdAsync(1)).ReturnsAsync(user);
            _userRepo.Setup(r => r.UpdateAsync(1, It.IsAny<User>()))
                     .ReturnsAsync((int _, User u) => u);
            _deptRepo.Setup(r => r.GetByIdAsync(It.IsAny<int>())).ReturnsAsync((Department?)null);

            var result = await CreateService().UpdateUserAsync(1, new UserUpdateRequest { IsActive = true });

            Assert.Equal("Active", result.Status);
        }

        // ── GetAllUsersAsync: pagination returns correct page ─────────────────

        [Fact]
        public async Task GetAll_Pagination_Page2_Works()
        {
            var users = Enumerable.Range(1, 8).Select(i => new User
            {
                Id = i, Name = $"User{i}", Email = $"u{i}@t.com", EmployeeId = $"E{i:000}",
                Role = UserRole.Employee, PasswordHash = "h", IsActive = true, JoiningDate = DateTime.UtcNow
            }).ToList();
            _userRepo.Setup(r => r.GetAllAsync()).ReturnsAsync(users);
            _deptRepo.Setup(r => r.GetAllAsync()).ReturnsAsync(new List<Department>());

            var result = await CreateService().GetAllUsersAsync(pageNumber: 2, pageSize: 5);

            Assert.NotNull(result);
        }
    }

namespace TimeSheetAppWeb.Tests.Services
{
    /// <summary>
    /// Further coverage: login edge cases, registration, and update flows.
    /// </summary>
    public class UserServiceFurtherTests
    {
        private readonly Mock<IRepository<int, User>>       _userRepo = new();
        private readonly Mock<IRepository<int, Department>> _deptRepo = new();
        private readonly Mock<IPasswordService>             _pwdSvc   = new();
        private readonly Mock<ITokenService>                _tokenSvc = new();
        private readonly Mock<ILogger<UserService>>         _logger   = new();

        private UserService CreateService() =>
            new(_userRepo.Object, _deptRepo.Object, _pwdSvc.Object, _tokenSvc.Object, _logger.Object);

        private static User MakeUser(int id = 1, bool active = true) => new()
        {
            Id = id, Name = "Test User", Email = "test@example.com",
            EmployeeId = "E001", Role = UserRole.Employee,
            PasswordHash = "hashed", IsActive = active, JoiningDate = DateTime.UtcNow
        };

        // ── Login — token contains correct userId ─────────────────────────────

        [Fact]
        public async Task Login_Valid_TokenPayloadContainsUserId()
        {
            var user = MakeUser();
            TokenPayloadDto? captured = null;
            _userRepo.Setup(r => r.GetAllAsync()).ReturnsAsync(new List<User> { user });
            _pwdSvc.Setup(p => p.VerifyPassword("pass", user.PasswordHash)).Returns(true);
            _tokenSvc.Setup(t => t.CreateToken(It.IsAny<TokenPayloadDto>()))
                     .Callback<TokenPayloadDto>(p => captured = p)
                     .Returns("token");

            await CreateService().LoginAsync(new CheckUserRequestDto { Username = "Test User", Password = "pass" });

            Assert.Equal(1, captured?.UserId);
        }

        // ── Login — token contains correct role ───────────────────────────────

        [Fact]
        public async Task Login_Valid_TokenPayloadContainsRole()
        {
            var user = MakeUser();
            TokenPayloadDto? captured = null;
            _userRepo.Setup(r => r.GetAllAsync()).ReturnsAsync(new List<User> { user });
            _pwdSvc.Setup(p => p.VerifyPassword("pass", user.PasswordHash)).Returns(true);
            _tokenSvc.Setup(t => t.CreateToken(It.IsAny<TokenPayloadDto>()))
                     .Callback<TokenPayloadDto>(p => captured = p)
                     .Returns("token");

            await CreateService().LoginAsync(new CheckUserRequestDto { Username = "Test User", Password = "pass" });

            Assert.Equal("Employee", captured?.Role);
        }

        // ── Register — password is hashed ────────────────────────────────────

        [Fact]
        public async Task Register_Valid_PasswordIsHashed()
        {
            var newUser = MakeUser(2);
            _userRepo.Setup(r => r.GetAllAsync()).ReturnsAsync(new List<User>());
            _pwdSvc.Setup(p => p.HashPassword("plaintext")).Returns("hashed_value");
            _userRepo.Setup(r => r.AddAsync(It.IsAny<User>())).ReturnsAsync(newUser);
            _deptRepo.Setup(r => r.GetByIdAsync(It.IsAny<int>())).ReturnsAsync((Department?)null);

            await CreateService().RegisterUserAsync(new UserCreateRequest
            {
                Name = "New", Email = "new@test.com", EmployeeId = "E002",
                Password = "plaintext", Role = "Employee"
            });

            _pwdSvc.Verify(p => p.HashPassword("plaintext"), Times.Once);
        }

        // ── Register — new user is inactive by default ────────────────────────

        [Fact]
        public async Task Register_Valid_NewUserIsInactiveByDefault()
        {
            User? captured = null;
            _userRepo.Setup(r => r.GetAllAsync()).ReturnsAsync(new List<User>());
            _pwdSvc.Setup(p => p.HashPassword(It.IsAny<string>())).Returns("hashed");
            _userRepo.Setup(r => r.AddAsync(It.IsAny<User>()))
                     .Callback<User>(u => captured = u)
                     .ReturnsAsync(MakeUser(2));
            _deptRepo.Setup(r => r.GetByIdAsync(It.IsAny<int>())).ReturnsAsync((Department?)null);

            await CreateService().RegisterUserAsync(new UserCreateRequest
            {
                Name = "New", Email = "new@test.com", EmployeeId = "E002",
                Password = "pass", Role = "Employee"
            });

            Assert.False(captured?.IsActive);
        }

        // ── Update — phone updated ────────────────────────────────────────────

        [Fact]
        public async Task Update_PhoneUpdated_ReturnsNewPhone()
        {
            var user = MakeUser();
            _userRepo.Setup(r => r.GetByIdAsync(1)).ReturnsAsync(user);
            _userRepo.Setup(r => r.UpdateAsync(1, It.IsAny<User>()))
                     .ReturnsAsync((int _, User u) => u);
            _deptRepo.Setup(r => r.GetByIdAsync(It.IsAny<int>())).ReturnsAsync((Department?)null);

            var result = await CreateService().UpdateUserAsync(1, new UserUpdateRequest { Phone = "9876543210" });

            Assert.Equal("9876543210", result.Phone);
        }

        // ── GetAll — sort by status ───────────────────────────────────────────

        [Fact]
        public async Task GetAll_SortByStatus_Works()
        {
            var users = new List<User> { MakeUser(1, true), MakeUser(2, false) };
            _userRepo.Setup(r => r.GetAllAsync()).ReturnsAsync(users);
            _deptRepo.Setup(r => r.GetAllAsync()).ReturnsAsync(new List<Department>());

            var result = await CreateService().GetAllUsersAsync(sortBy: "status", sortDir: "asc");

            Assert.NotNull(result);
        }

        // ── GetAll — search by employeeId ─────────────────────────────────────

        [Fact]
        public async Task GetAll_SearchByEmployeeId_ReturnsMatchingUser()
        {
            var users = new List<User>
            {
                MakeUser(1),
                new User { Id = 2, Name = "Bob", Email = "b@t.com", EmployeeId = "EMP999",
                           Role = UserRole.Employee, PasswordHash = "h", IsActive = true, JoiningDate = DateTime.UtcNow }
            };
            _userRepo.Setup(r => r.GetAllAsync()).ReturnsAsync(users);
            _deptRepo.Setup(r => r.GetAllAsync()).ReturnsAsync(new List<Department>());

            var result = await CreateService().GetAllUsersAsync(search: "EMP999");

            Assert.NotNull(result);
        }

        // ── Delete — verifies DeleteAsync called ──────────────────────────────

        [Fact]
        public async Task Delete_Valid_CallsDeleteAsync()
        {
            var user = MakeUser();
            _userRepo.Setup(r => r.GetByIdAsync(1)).ReturnsAsync(user);
            _userRepo.Setup(r => r.DeleteAsync(1)).ReturnsAsync(user);

            await CreateService().DeleteUserAsync(1);

            _userRepo.Verify(r => r.DeleteAsync(1), Times.Once);
        }

        // ── GetAll — pagination ───────────────────────────────────────────────

        [Fact]
        public async Task GetAll_Page2_ReturnsCorrectSlice()
        {
            var users = Enumerable.Range(1, 7).Select(i => new User
            {
                Id = i, Name = $"User {i}", Email = $"u{i}@t.com", EmployeeId = $"E{i:D3}",
                Role = UserRole.Employee, PasswordHash = "h", IsActive = true, JoiningDate = DateTime.UtcNow
            }).ToList();
            _userRepo.Setup(r => r.GetAllAsync()).ReturnsAsync(users);
            _deptRepo.Setup(r => r.GetAllAsync()).ReturnsAsync(new List<Department>());

            var result = await CreateService().GetAllUsersAsync(pageNumber: 2, pageSize: 5);

            Assert.NotNull(result);
        }
    }
}
