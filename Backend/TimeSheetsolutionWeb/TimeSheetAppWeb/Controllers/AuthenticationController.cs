using Microsoft.AspNetCore.Mvc;
using TimeSheetAppWeb.Exceptions;
using TimeSheetAppWeb.Interfaces;
using TimeSheetAppWeb.Model.DTOs;
using TimeSheetAppWeb.Models.DTOs;

namespace TimeSheetAppWeb.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class AuthenticationController : ControllerBase
    {
        private readonly IUserService _userService;

        public AuthenticationController(IUserService userService)
        {
            _userService = userService;
        }

        [HttpPost("login")]
        public async Task<IActionResult> Login(CheckUserRequestDto request)
        {
            try
            {
                var result = await _userService.LoginAsync(request);
                return Ok(result);
            }
            catch (UnAuthorizedException ex)
            {
                return Unauthorized(new
                {
                    code = "INVALID_CREDENTIALS",
                    message = ex.Message
                }); // 401
            }
            catch (InvalidOperationException ex)
            {
                // 🔥 Handle different business cases
                if (ex.Message.Contains("inactive", StringComparison.OrdinalIgnoreCase))
                {
                    return BadRequest(new
                    {
                        code = "USER_INACTIVE",
                        message = ex.Message
                    });
                }

                if (ex.Message.Contains("pending", StringComparison.OrdinalIgnoreCase) ||
                    ex.Message.Contains("approval", StringComparison.OrdinalIgnoreCase))
                {
                    return BadRequest(new
                    {
                        code = "USER_PENDING",
                        message = ex.Message
                    });
                }

                return BadRequest(new
                {
                    code = "BUSINESS_ERROR",
                    message = ex.Message
                });
            }
            catch (Exception ex)
            {
                // 🔥 VERY IMPORTANT (prevents raw 500 crash)
                return StatusCode(500, new
                {
                    code = "SERVER_ERROR",
                    message = "Something went wrong. Please try again later."
                });
            }
        }
        [HttpPost("register")]
        public async Task<ActionResult<UserResponse>> Register([FromBody] UserCreateRequest request)
        {
            try
            {
                var result = await _userService.RegisterUserAsync(request);
                return Ok(result);
            }
            catch (Exception ex)
            {
                return BadRequest(new { message = ex.Message });
            }
        }
    }
}