using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;
using TimeSheetAppWeb.Interface;
using TimeSheetAppWeb.Interfaces;
using TimeSheetAppWeb.Model.DTOs;

namespace TimeSheetAppWeb.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    [Authorize] // Require authentication for all actions
    public class UserController : ControllerBase
    {
        private readonly IUserService _userService;

        public UserController(IUserService userService)
        {
            _userService = userService;
        }

        // ---------------- GET USER BY ID ----------------
        [HttpGet("{userId}")]
        [Authorize(Roles = "Admin,HR,Manager,Employee,Intern,Mentor")]
        public async Task<IActionResult> GetUserById(int userId)
        {
            try
            {
                var user = await _userService.GetUserByIdAsync(userId);
                return Ok(new { Success = true, Data = user });
            }
            catch (KeyNotFoundException knfEx)
            {
                return NotFound(new { Success = false, Message = knfEx.Message });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { Success = false, Message = "Error fetching user.", Details = ex.Message });
            }
        }
        [Authorize]
        [HttpGet("me")]
        public async Task<IActionResult> GetMyProfile()
        {
            var result = await _userService.GetMyProfileAsync(User);
            return Ok(result);
        }
        // ---------------- GET ALL USERS ----------------
        [HttpGet]
        [Authorize(Roles = "Admin,HR,Manager,Mentor")]
        public async Task<IActionResult> GetAllUsers([FromQuery] int pageNumber = 1, [FromQuery] int pageSize = 10)
        {
            try
            {
                var result = await _userService.GetAllUsersAsync(pageNumber, pageSize);
                return Ok(result);
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { Success = false, Message = $"Error fetching users: {ex.Message}" });
            }
        }

        // ---------------- UPDATE USER ----------------
        [HttpPut("{userId}")]
        [Authorize(Roles = "Admin,HR,Manager,Mentor")]
        public async Task<IActionResult> UpdateUser(int userId, [FromBody] UserUpdateRequest request)
        {
            try
            {
                var updatedUser = await _userService.UpdateUserAsync(userId, request);
                return Ok(new { Success = true, Data = updatedUser });
            }
            catch (KeyNotFoundException knfEx)
            {
                return NotFound(new { Success = false, Message = knfEx.Message });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { Success = false, Message = "Error updating user.", Details = ex.Message });
            }
        }

        // ---------------- DELETE USER ----------------
        [HttpDelete("{userId}")]
        [Authorize(Roles = "Admin,HR")]
        public async Task<IActionResult> DeleteUser(int userId)
        {
            try
            {
                var result = await _userService.DeleteUserAsync(userId);
                if (!result)
                    return NotFound(new { Success = false, Message = "User not found or already deleted." });

                return Ok(new { Success = true, Message = "User deleted successfully." });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { Success = false, Message = "Error deleting user.", Details = ex.Message });
            }
        }
    }
}