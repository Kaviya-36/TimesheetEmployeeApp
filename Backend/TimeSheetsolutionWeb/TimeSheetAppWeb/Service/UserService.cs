using System.Security.Claims;
using TimeSheetAppWeb.Exceptions;
using TimeSheetAppWeb.Interface;
using TimeSheetAppWeb.Interfaces;
using TimeSheetAppWeb.Model;
using TimeSheetAppWeb.Model.DTOs;
using TimeSheetAppWeb.Models.DTOs;

namespace TimeSheetAppWeb.Services
{
    public class UserService : IUserService
    {
        private readonly IRepository<int, User> _userRepository;
        private readonly IRepository<int, Department> _departmentRepository;
        private readonly IPasswordService _passwordService;
        private readonly ITokenService _tokenService;
        private readonly ILogger<UserService> _logger;

        public UserService(
            IRepository<int, User> userRepository,
            IRepository<int, Department> departmentRepository,
            IPasswordService passwordService,
            ITokenService tokenService,
            ILogger<UserService> logger)
        {
            _userRepository = userRepository ?? throw new ArgumentNullException(nameof(userRepository));
            _departmentRepository = departmentRepository ?? throw new ArgumentNullException(nameof(departmentRepository));
            _passwordService = passwordService ?? throw new ArgumentNullException(nameof(passwordService));
            _tokenService = tokenService ?? throw new ArgumentNullException(nameof(tokenService));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        // ---------------- LOGIN ----------------
        public async Task<CheckUserResponseDto> LoginAsync(CheckUserRequestDto request)
        {
            _logger.LogInformation("Login attempt for user {Username}", request.Username);

            var users = await _userRepository.GetAllAsync() ?? Enumerable.Empty<User>();

            var user = users.FirstOrDefault(u =>
                u.Name.Equals(request.Username, StringComparison.OrdinalIgnoreCase));

            if (user == null || !_passwordService.VerifyPassword(request.Password, user.PasswordHash))
            {
                _logger.LogWarning("Unauthorized login attempt for user {Username}", request.Username);
                throw new UnAuthorizedException("Invalid username or password");
            }

            if (!user.IsActive)
            {
                _logger.LogWarning("Inactive user login attempt: {Username}", request.Username);
                throw new InvalidOperationException("User is inactive");
            }

            var token = _tokenService.CreateToken(new TokenPayloadDto
            {
                UserId = user.Id,
                Username = user.Name,
                Role = user.Role.ToString()
            });

            _logger.LogInformation("User {Username} logged in successfully", request.Username);

            return new CheckUserResponseDto
            {
                Token = token
            };
        }

        // ---------------- REGISTER ----------------
        public async Task<UserResponse> RegisterUserAsync(UserCreateRequest request)
        {
            try
            {
                _logger.LogInformation("Registering new user with Email: {Email} and EmployeeId: {EmployeeId}",
                    request.Email, request.EmployeeId);

                var users = await _userRepository.GetAllAsync() ?? Enumerable.Empty<User>();

                if (users.Any(u => u.Email.Equals(request.Email, StringComparison.OrdinalIgnoreCase)))
                {
                    _logger.LogWarning("Registration failed: Email {Email} already exists", request.Email);
                    throw new InvalidOperationException("Email already exists");
                }

                if (users.Any(u => u.EmployeeId.Equals(request.EmployeeId, StringComparison.OrdinalIgnoreCase)))
                {
                    _logger.LogWarning("Registration failed: EmployeeId {EmployeeId} already exists", request.EmployeeId);
                    throw new InvalidOperationException("EmployeeId already exists");
                }

                if (users.Any(u => u.Name.Equals(request.Name, StringComparison.OrdinalIgnoreCase)))
                {
                    _logger.LogWarning("Registration failed: Name {Name} already exists", request.Name);
                    throw new InvalidOperationException("Username already exists");
                }

                var hashedPassword = _passwordService.HashPassword(request.Password);

                var user = new User
                {
                    EmployeeId = request.EmployeeId,
                    Name = request.Name,
                    Email = request.Email,
                    Phone = request.Phone,
                    DepartmentId = request.DepartmentId,
                    Role = Enum.TryParse<UserRole>(request.Role, true, out var role) ? role : UserRole.Employee,
                    PasswordHash = hashedPassword,
                    IsActive = false,
                    JoiningDate = DateTime.UtcNow
                };

                var addedUser = await _userRepository.AddAsync(user);

                _logger.LogInformation("User {Username} registered successfully with ID {UserId}",
                    user.Name, addedUser?.Id);

                var deptName = await ResolveDepartmentNameAsync(addedUser!.DepartmentId);
                return MapToDto(addedUser!, deptName);
            }
            catch (InvalidOperationException)
            {
                throw; // re-throw business validation errors as-is
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error occurred while registering user with Email {Email}", request.Email);
                throw new Exception(ex.InnerException?.Message ?? ex.Message);
            }
        }

        // ---------------- GET USER BY ID ----------------
        public async Task<UserResponse> GetUserByIdAsync(int userId)
        {
            try
            {
                _logger.LogInformation("Fetching user with ID {UserId}", userId);

                var user = await _userRepository.GetByIdAsync(userId)
                           ?? throw new KeyNotFoundException("User not found");

                _logger.LogInformation("User {Username} retrieved successfully", user.Name);

                var deptName = await ResolveDepartmentNameAsync(user.DepartmentId);
                return MapToDto(user, deptName);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error occurred while fetching user with ID {UserId}", userId);
                throw new Exception($"An error occurred while fetching user with ID {userId}.", ex);
            }
        }

        // ---------------- GET ALL USERS ----------------
        public async Task<object> GetAllUsersAsync(int pageNumber = 1, int pageSize = 10, string? search = null, string? role = null, string? status = null, string? sortBy = "name", string? sortDir = "asc")
        {
            try
            {
                var users = await _userRepository.GetAllAsync() ?? Enumerable.Empty<User>();
                var depts = await _departmentRepository.GetAllAsync() ?? Enumerable.Empty<Department>();
                var deptMap = depts.ToDictionary(d => d.Id, d => d.Name);

                var filtered = users.AsEnumerable();
                if (!string.IsNullOrWhiteSpace(search))
                {
                    var q = search.ToLower();
                    filtered = filtered.Where(u => u.Name.ToLower().Contains(q) || u.Email.ToLower().Contains(q) || u.EmployeeId.ToLower().Contains(q));
                }
                if (!string.IsNullOrWhiteSpace(role))
                    filtered = filtered.Where(u => u.Role.ToString().ToLower() == role.ToLower());
                if (!string.IsNullOrWhiteSpace(status))
                    filtered = filtered.Where(u => status.ToLower() == "active" ? u.IsActive : !u.IsActive);

                filtered = (sortBy?.ToLower(), sortDir?.ToLower()) switch
                {
                    ("role",   "desc") => filtered.OrderByDescending(u => u.Role.ToString()),
                    ("role",   _)      => filtered.OrderBy(u => u.Role.ToString()),
                    ("joined", "desc") => filtered.OrderByDescending(u => u.JoiningDate),
                    ("joined", _)      => filtered.OrderBy(u => u.JoiningDate),
                    ("name",   "desc") => filtered.OrderByDescending(u => u.Name),
                    _                  => filtered.OrderBy(u => u.Name)
                };

                var total = filtered.Count();
                var paged = filtered.Skip((pageNumber - 1) * pageSize).Take(pageSize)
                    .Select(u => MapToDto(u, u.DepartmentId.HasValue && deptMap.TryGetValue(u.DepartmentId.Value, out var n) ? n : null))
                    .ToList();

                _logger.LogInformation("Retrieved {Count} users", paged.Count);
                return new { success = true, data = new { data = paged, totalRecords = total, pageNumber, pageSize, totalPages = (int)Math.Ceiling(total / (double)pageSize) } };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error occurred while fetching all users");
                throw new Exception("An error occurred while fetching all users.", ex);
            }
        }

        // ---------------- UPDATE USER ----------------
        public async Task<UserResponse> UpdateUserAsync(int userId, UserUpdateRequest request)
        {
            try
            {
                _logger.LogInformation("Updating user with ID {UserId}", userId);

                var user = await _userRepository.GetByIdAsync(userId)
                           ?? throw new KeyNotFoundException("User not found");

                if (!string.IsNullOrEmpty(request.Name)) user.Name = request.Name;
                if (!string.IsNullOrEmpty(request.Email)) user.Email = request.Email;
                if (!string.IsNullOrEmpty(request.Phone)) user.Phone = request.Phone;
                if (request.DepartmentId.HasValue) user.DepartmentId = request.DepartmentId.Value;
                if (!string.IsNullOrEmpty(request.Role) &&
                    Enum.TryParse<UserRole>(request.Role, true, out var role)) user.Role = role;
                if (request.IsActive.HasValue) user.IsActive = request.IsActive.Value;

                var updatedUser = await _userRepository.UpdateAsync(userId, user);

                _logger.LogInformation("User with ID {UserId} updated successfully", userId);

                var deptName = await ResolveDepartmentNameAsync(updatedUser!.DepartmentId);
                return MapToDto(updatedUser!, deptName);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error occurred while updating user with ID {UserId}", userId);
                throw new Exception($"An error occurred while updating user with ID {userId}.", ex);
            }
        }
        public async Task<UserResponse> GetMyProfileAsync(ClaimsPrincipal userClaims)
        {
            try
            {
                var userIdClaim = userClaims.FindFirst(ClaimTypes.NameIdentifier)?.Value;

                if (string.IsNullOrEmpty(userIdClaim))
                    throw new UnAuthorizedException("Invalid token");

                var userId = int.Parse(userIdClaim);

                var user = await _userRepository.GetByIdAsync(userId)
                           ?? throw new KeyNotFoundException("User not found");

                _logger.LogInformation("Fetched profile for user {UserId}", userId);

                var deptName = await ResolveDepartmentNameAsync(user.DepartmentId);
                return MapToDto(user, deptName);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error fetching profile");
                throw;
            }
        }

        // ---------------- DELETE USER ----------------
        public async Task<bool> DeleteUserAsync(int userId)
        {
            try
            {
                _logger.LogInformation("Deleting user with ID {UserId}", userId);

                var user = await _userRepository.GetByIdAsync(userId);

                if (user == null)
                {
                    _logger.LogWarning("Delete failed: User with ID {UserId} not found", userId);
                    return false;
                }

                await _userRepository.DeleteAsync(userId);

                _logger.LogInformation("User with ID {UserId} deleted successfully", userId);
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error occurred while deleting user with ID {UserId}", userId);
                throw new Exception($"An error occurred while deleting user with ID {userId}.", ex);
            }
        }

        // ---------------- HELPER: MAP USER TO DTO ----------------
        private UserResponse MapToDto(User user, string? departmentName = null)
        {
            return new UserResponse
            {
                Id = user.Id,
                EmployeeId = user.EmployeeId,
                Name = user.Name,
                Email = user.Email,
                Phone = user.Phone ?? string.Empty,
                Role = user.Role.ToString(),
                Status = user.IsActive ? "Active" : "Inactive",
                JoiningDate = user.JoiningDate,
                DepartmentName = departmentName
            };
        }

        private async Task<string?> ResolveDepartmentNameAsync(int? departmentId)
        {
            if (departmentId == null) return null;
            var dept = await _departmentRepository.GetByIdAsync(departmentId.Value);
            return dept?.Name;
        }
    }
}