using System.Collections.Generic;
using System.Security.Claims;
using System.Threading.Tasks;
using TimeSheetAppWeb.Model;
using TimeSheetAppWeb.Model.DTOs;
using TimeSheetAppWeb.Models.DTOs;

namespace TimeSheetAppWeb.Interfaces
{
    public interface IUserService
    {
        Task<CheckUserResponseDto> LoginAsync(CheckUserRequestDto request);
        Task<UserResponse> RegisterUserAsync(UserCreateRequest request);
        Task<UserResponse> GetUserByIdAsync(int userId);
        Task<object> GetAllUsersAsync(int pageNumber = 1, int pageSize = 10, string? search = null, string? role = null, string? status = null, string? sortBy = "name", string? sortDir = "asc");
        Task<UserResponse> UpdateUserAsync(int userId, UserUpdateRequest request);
        Task<bool> DeleteUserAsync(int userId);
        Task<UserResponse> GetMyProfileAsync(ClaimsPrincipal userClaims);

    }
}