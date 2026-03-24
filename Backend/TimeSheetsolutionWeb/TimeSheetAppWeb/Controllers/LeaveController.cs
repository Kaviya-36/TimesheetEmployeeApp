using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;
using TimeSheetAppWeb.Interface;
using TimeSheetAppWeb.Model.DTOs;

namespace TimeSheetAppWeb.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    [Authorize]
    public class LeaveController : ControllerBase
    {
        private readonly ILeaveService _leaveService;

        public LeaveController(ILeaveService leaveService)
        {
            _leaveService = leaveService;
        }

        // ================= GET LEAVE TYPES =================
        [HttpGet("types")]
        [Authorize(Roles = "Employee,Intern,HR,Manager,Admin")]
        public async Task<IActionResult> GetLeaveTypes()
        {
            var response = await _leaveService.GetLeaveTypesAsync();
            return Ok(response);
        }

        // ================= APPLY LEAVE =================
        [HttpPost("apply")]
        [Authorize(Roles = "Employee,Intern,HR,Manager,Admin")]
        public async Task<IActionResult> ApplyLeave([FromBody] LeaveCreateRequest request)
        {
            try
            {
                var userId = int.Parse(User.FindFirst(ClaimTypes.NameIdentifier)!.Value);

                var response = await _leaveService.ApplyLeaveAsync(userId, request);

                if (!response.Success)
                    return BadRequest(response);

                return Ok(response);
            }
            catch (Exception ex)
            {
                return StatusCode(500, new
                {
                    Success = false,
                    Message = $"Error applying leave: {ex.Message}"
                });
            }
        }

        // ================= APPROVE / REJECT =================
        [HttpPut("approve")]
        [Authorize(Roles = "Admin,HR,Manager")]
        public async Task<IActionResult> ApproveOrRejectLeave([FromBody] LeaveApprovalRequest request)
        {
            try
            {
                var response = await _leaveService.ApproveOrRejectLeaveAsync(request);

                if (!response.Success)
                    return BadRequest(response);

                return Ok(response);
            }
            catch (Exception ex)
            {
                return StatusCode(500, new
                {
                    Success = false,
                    Message = $"Error updating leave: {ex.Message}"
                });
            }
        }

        // ================= GET MY LEAVES =================
        [HttpGet("user/{userId}")]
        [Authorize(Roles = "Employee,Intern,HR,Manager,Admin")]
        public async Task<IActionResult> GetMyLeaves(
            [FromQuery] int pageNumber = 1,
            [FromQuery] int pageSize = 10)
        {
            var userId = int.Parse(User.FindFirst(ClaimTypes.NameIdentifier)!.Value);

            var response = await _leaveService.GetUserLeavesAsync(userId, pageNumber, pageSize);

            if (!response.Success)
                return NotFound(response);

            return Ok(response);
        }

        // ================= GET ALL LEAVES =================
        [HttpGet("getall")]
        [Authorize(Roles = "Admin,HR,Manager")]
        public async Task<IActionResult> GetAllLeaves(
            [FromQuery] int pageNumber = 1,
            [FromQuery] int pageSize = 10)
        {
            var response = await _leaveService.GetAllLeavesAsync(pageNumber, pageSize);

            if (!response.Success)
                return NotFound(response);

            return Ok(response);
        }
    }
}