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

        // ---------------- GET USER TIMESHEETS (With Pagination + Filter + Sort) ----------------
        [HttpGet("user/{userId}")]
        [Authorize(Roles = "Intern,Employee,Manager,HR,Admin")]
        public async Task<IActionResult> GetUserTimesheets(int userId,
            [FromQuery] int pageNumber = 1, [FromQuery] int pageSize = 10,
            [FromQuery] string? search = null, [FromQuery] string? status = null,
            [FromQuery] string? sortBy = "date", [FromQuery] string? sortDir = "desc")
        {
            var result = await _timesheetService.GetUserTimesheetsAsync(userId, new PaginationParams
            {
                PageNumber = pageNumber, PageSize = pageSize,
                Search = search, Status = status, SortBy = sortBy, SortDir = sortDir
            });
            return Ok(result);
        }

        // ---------------- GET ALL TIMESHEETS (With Pagination + Filter + Sort) ----------------
        [HttpGet]
        [Authorize(Roles = "Admin,HR,Manager")]
        public async Task<IActionResult> GetAllTimesheets(
            [FromQuery] int pageNumber = 1, [FromQuery] int pageSize = 10,
            [FromQuery] string? search = null, [FromQuery] string? status = null,
            [FromQuery] string? sortBy = "date", [FromQuery] string? sortDir = "desc")
        {
            var result = await _timesheetService.GetAllTimesheetsAsync(new PaginationParams
            {
                PageNumber = pageNumber, PageSize = pageSize,
                Search = search, Status = status, SortBy = sortBy, SortDir = sortDir
            });
            return Ok(result);
        }
        //---------------Create Timesheet (manual with start/end time)--------------------
        [Authorize(Roles = "Employee,Intern,Manager,Mentor,HR,Admin")]
        [HttpPost("{userId}/manual")]
        public async Task<IActionResult> CreateTimesheet(int userId, [FromBody] TimesheetCreateRequest request)
        {
            var result = await _timesheetService.CreateManualTimesheetAsync(userId, request);
            if (!result.Success) return BadRequest(result);
            return Ok(result);
        }

        //---------------Create Timesheet from Grid (hours only) ----------------
        [Authorize(Roles = "Employee,Intern,Manager,Mentor,HR,Admin")]
        [HttpPost("{userId}/grid")]
        public async Task<IActionResult> CreateTimesheetFromGrid(int userId, [FromBody] TimesheetGridRequest request)
        {
            if (!ModelState.IsValid)
                return BadRequest(new { Success = false, Message = "Invalid request", Errors = ModelState.Values.SelectMany(v => v.Errors).Select(e => e.ErrorMessage) });

            var result = await _timesheetService.CreateFromGridAsync(userId, request);
            if (!result.Success) return BadRequest(result);
            return Ok(result);
        }

        //--------------- Submit Weekly Timesheet (batch) ----------------
        [Authorize(Roles = "Employee,Intern,Manager,Mentor,HR,Admin")]
        [HttpPost("{userId}/weekly")]
        public async Task<IActionResult> SubmitWeeklyTimesheet(int userId, [FromBody] TimesheetWeeklyRequest request)
        {
            if (!ModelState.IsValid)
                return BadRequest(new { Success = false, Message = "Invalid request" });

            var result = await _timesheetService.SubmitWeeklyAsync(userId, request);
            if (!result.Success) return BadRequest(result);
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