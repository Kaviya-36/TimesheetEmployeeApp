using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TimeSheetAppWeb.Interface;
using TimeSheetAppWeb.Model.DTOs;

namespace TimeSheetAppWeb.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    [Authorize(Roles = "Employee,Intern,Admin,HR,Manager")]
    public class AnalyticsController : ControllerBase
    {
        private readonly IAnalyticsService _analyticsService;

        public AnalyticsController(IAnalyticsService analyticsService)
        {
            _analyticsService = analyticsService;
        }

        [HttpGet("timesheet")]
        public async Task<IActionResult> GetTimesheetAnalytics([FromQuery] AnalyticsRequest request)
        {
            try
            {
                var analytics = await _analyticsService.GetTimesheetAnalyticsAsync(request);
                return Ok(analytics);
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { Success = false, Message = ex.Message });
            }
        }

        [HttpGet("dashboard")]
        [Authorize(Roles = "Employee,Intern,Admin,HR,Manager")]
        public async Task<IActionResult> GetDashboardSummary([FromQuery] AnalyticsRequest request)
        {
            try
            {
                var summary = await _analyticsService.GetDashboardSummaryAsync(request);
                return Ok(summary);
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { Success = false, Message = ex.Message });
            }
        }
    }
}
