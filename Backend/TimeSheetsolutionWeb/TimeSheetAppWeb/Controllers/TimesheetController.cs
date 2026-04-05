using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;
using TimeSheetAppWeb.Interface;
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

        public TimesheetController(ITimesheetService timesheetService)
        {
            _timesheetService = timesheetService;
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
            var callerId = int.Parse(User.FindFirst(ClaimTypes.NameIdentifier)!.Value);
            var callerRole = User.FindFirst(ClaimTypes.Role)?.Value
                          ?? User.FindFirst("http://schemas.microsoft.com/ws/2008/06/identity/claims/role")?.Value;
            var result = await _timesheetService.GetAllTimesheetsAsync(new PaginationParams
            {
                PageNumber = pageNumber, PageSize = pageSize,
                Search = search, Status = status, SortBy = sortBy, SortDir = sortDir
            }, callerId, callerRole);
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