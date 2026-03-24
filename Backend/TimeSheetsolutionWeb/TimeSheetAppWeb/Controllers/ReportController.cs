using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TimeSheetAppWeb.Interface;
using TimeSheetAppWeb.Model.DTOs;

namespace TimeSheetAppWeb.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    [Authorize(Roles = "Admin,HR,Manager")]
    public class ReportController : ControllerBase
    {
        private readonly IReportService _reportService;

        public ReportController(IReportService reportService)
        {
            _reportService = reportService;
        }

        [HttpGet("timesheet")]
        public async Task<IActionResult> GetTimesheetReport([FromQuery] ReportRequest request)
        {
            try
            {
                var report = await _reportService.GetTimesheetReportAsync(request);
                return Ok(report);
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { Success = false, Message = ex.Message });
            }
        }
    }
}
