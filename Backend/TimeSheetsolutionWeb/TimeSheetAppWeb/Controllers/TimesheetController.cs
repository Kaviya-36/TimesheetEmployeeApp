using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TimeSheetAppWeb.Interface;
using TimeSheetAppWeb.Model;
using TimeSheetAppWeb.Model.common;
using TimeSheetAppWeb.Model.DTOs;

namespace TimeSheetAppWeb.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    [Authorize]
    public class TimesheetController : ControllerBase
    {
        private readonly ITimesheetService _timesheetService;
        private readonly IAttendanceService _attendanceService;

        public TimesheetController(ITimesheetService timesheetService, IAttendanceService attendanceService)
        {
            _timesheetService = timesheetService;
            _attendanceService = attendanceService;
        }

        // ---------------- CREATE TIMESHEET FROM ATTENDANCE (Today Only) ----------------
        [HttpPost("{userId}")]
        [Authorize(Roles = "Employee,Intern,Manager,Mentor,HR,Admin")]
        public async Task<IActionResult> CreateTimesheetFromAttendance(int userId)
        {
            try
            {
                var attendances = await _attendanceService.GetUserAttendanceEntitiesAsync(userId);

                var todayAttendance = attendances.FirstOrDefault(a => a.Date.Date == DateTime.Today);
                if (todayAttendance == null)
                    return BadRequest(new { Success = false, Message = "No attendance found for today." });

                var timesheetResult = await _timesheetService.CreateTimesheetFromAttendanceAsync(userId, todayAttendance);
                if (!timesheetResult.Success)
                    return BadRequest(timesheetResult);

                return Ok(timesheetResult);
            }
            catch (Exception ex)
            {
                return StatusCode(500, new
                {
                    Success = false,
                    Message = "An error occurred while creating the timesheet from attendance.",
                    Details = ex.Message
                });
            }
        }
        // ---------------- UPDATE TIMESHEET ----------------
        [HttpPut("{timesheetId}")]
        [Authorize(Roles = "Employee,Manager,HR,Admin,Intern")]
        public async Task<IActionResult> UpdateTimesheet(int timesheetId, [FromBody] TimesheetUpdateRequest request)
        {
            try
            {
                var result = await _timesheetService.UpdateTimesheetAsync(timesheetId, request);
                if (!result.Success) return BadRequest(result);
                return Ok(result);
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { Success = false, Message = "An error occurred while updating the timesheet.", Details = ex.Message });
            }
        }

        // ---------------- DELETE TIMESHEET ----------------
        [HttpDelete("{timesheetId}")]
        [Authorize(Roles = "Admin,HR")]
        public async Task<IActionResult> DeleteTimesheet(int timesheetId)
        {
            try
            {
                var result = await _timesheetService.DeleteTimesheetAsync(timesheetId);
                if (!result.Success) return NotFound(result);
                return Ok(result);
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { Success = false, Message = "An error occurred while deleting the timesheet.", Details = ex.Message });
            }
        }

        // ---------------- GET USER TIMESHEETS (With Pagination) ----------------
        [HttpGet("user/{userId}")]
        [Authorize(Roles = "Intern,Employee,Manager,HR,Admin")]
        public async Task<IActionResult> GetUserTimesheets(int userId, [FromQuery] int pageNumber = 1, [FromQuery] int pageSize = 10)
        {
            try
            {
                var paginationParams = new PaginationParams
                {
                    PageNumber = pageNumber,
                    PageSize = pageSize
                };

                var result = await _timesheetService.GetUserTimesheetsAsync(userId, paginationParams);
                return Ok(result);
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { Success = false, Message = "An error occurred while retrieving user timesheets.", Details = ex.Message });
            }
        }

        // ---------------- GET ALL TIMESHEETS (With Pagination) ----------------
        [HttpGet]
        [Authorize(Roles = "Admin,HR,Manager")]
        public async Task<IActionResult> GetAllTimesheets([FromQuery] int pageNumber = 1, [FromQuery] int pageSize = 10)
        {
            try
            {
                var paginationParams = new PaginationParams
                {
                    PageNumber = pageNumber,
                    PageSize = pageSize
                };

                var result = await _timesheetService.GetAllTimesheetsAsync(paginationParams);
                return Ok(result);
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { Success = false, Message = "An error occurred while retrieving all timesheets.", Details = ex.Message });
            }
        }
        //---------------Create Timeshee--------------------
        [Authorize(Roles = "Employee,Intern,Manager,Mentor,HR,Admin")]
        [HttpPost("{userId}/manual")]
        public async Task<IActionResult> CreateTimesheet(int userId, [FromBody] TimesheetCreateRequest request)
        {
            var result = await _timesheetService.CreateManualTimesheetAsync(userId, request);

            if (!result.Success)
                return BadRequest(result);

            return Ok(result);
        }

        // ---------------- APPROVE OR REJECT ----------------
        [HttpPost("approve")]
        [Authorize(Roles = "Manager,HR")]
        public async Task<IActionResult> ApproveOrRejectTimesheet([FromBody] TimesheetApprovalRequest request)
        {
            try
            {
                var result = await _timesheetService.ApproveOrRejectTimesheetAsync(request);
                if (!result.Success) return BadRequest(result);
                return Ok(result);
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { Success = false, Message = "An error occurred while approving or rejecting the timesheet.", Details = ex.Message });
            }
        }
    }
}