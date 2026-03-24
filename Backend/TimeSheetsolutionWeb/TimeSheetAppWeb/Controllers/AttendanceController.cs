using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;
using TimeSheetAppWeb.Interface;
using TimeSheetAppWeb.Model.DTOs;

namespace TimeSheetAppWeb.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class AttendanceController : ControllerBase
    {
        private readonly IAttendanceService _attendanceService;

        public AttendanceController(IAttendanceService attendanceService)
        {
            _attendanceService = attendanceService;
        }

        // ---------------- CHECK IN ----------------
        [HttpPost("checkin")]
        [Authorize(Roles = "Employee,Intern,Manager,Mentor,HR,Admin")]
        public async Task<IActionResult> CheckIn()
        {
            try
            {
                var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier);
                if (userIdClaim == null)
                    return Unauthorized("Invalid token");

                var userId = int.Parse(userIdClaim.Value);
                var result = await _attendanceService.CheckInAsync(userId);

                if (!result.Success)
                    return BadRequest(result);

                return Ok(result);
            }
            catch (Exception ex)
            {
                // Log ex.Message if you have logging
                return StatusCode(500, new
                {
                    success = false,
                    message = "Internal server error",
                    details = ex.Message
                });
            }
        }

        // ---------------- CHECK OUT ----------------
        [HttpPost("checkout")]
        [Authorize(Roles = "Employee,Intern,Manager,Mentor,HR,Admin")]
        public async Task<IActionResult> CheckOut()
        {
            try
            {
                var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier);
                if (userIdClaim == null)
                    return Unauthorized("Invalid token");

                var userId = int.Parse(userIdClaim.Value);
                var result = await _attendanceService.CheckOutAsync(userId);

                if (!result.Success)
                    return BadRequest(result);

                return Ok(result);
            }
            catch (Exception ex)
            {
                return StatusCode(500, new
                {
                    success = false,
                    message = "Internal server error",
                    details = ex.Message
                });
            }
        }
        [HttpGet("today/{userId}")]
        public async Task<IActionResult> GetToday(int userId)
        {
            var result = await _attendanceService.GetTodayAsync(userId);
            return Ok(result);
        }

        // ---------------- GET USER ATTENDANCE ----------------
        [HttpGet("me")]
        [Authorize(Roles = "Employee,Intern,Manager,HR,Admin,Mentor")]
        public async Task<IActionResult> GetMyAttendance(int pageNumber = 1, int pageSize = 10)
        {
            try
            {
                var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier);
                if (userIdClaim == null)
                    return Unauthorized("Invalid token");

                var userId = int.Parse(userIdClaim.Value);

                var result = await _attendanceService.GetUserAttendanceAsync(userId, pageNumber, pageSize);

                if (!result.Success)
                    return NotFound(result);

                return Ok(result);
            }
            catch (Exception ex)
            {
                return StatusCode(500, new
                {
                    success = false,
                    message = "Internal server error",
                    details = ex.Message
                });
            }
        }
        // ---------------- GET ALL ATTENDANCE ----------------
        [HttpGet("all")]
        [Authorize(Roles = "Admin,HR,Manager")]
        public async Task<IActionResult> GetAllAttendance(int pageNumber = 1, int pageSize = 10)
        {
            try
            {
                var result = await _attendanceService.GetAllAttendanceAsync(pageNumber, pageSize);

                if (!result.Success)
                    return NotFound(result);

                return Ok(result);
            }
            catch (Exception ex)
            {
                return StatusCode(500, new
                {
                    success = false,
                    message = "Internal server error",
                    details = ex.Message
                });
            }
        }
    }
}