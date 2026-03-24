using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TimeSheetAppWeb.Interface;
using TimeSheetAppWeb.Model.DTOs;

namespace TimeSheetAppWeb.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    [Authorize] // Require JWT authentication
    public class PayrollController : ControllerBase
    {
        private readonly IPayrollService _payrollService;

        public PayrollController(IPayrollService payrollService)
        {
            _payrollService = payrollService;
        }

        // ---------------- CREATE PAYROLL ----------------
        [HttpPost]
        [Authorize(Roles = "Admin,Manager,HR")]
        public async Task<IActionResult> CreatePayroll([FromBody] PayrollCreateRequest request)
        {
            if (!ModelState.IsValid)
                return BadRequest(ModelState);

            var result = await _payrollService.CreatePayrollAsync(request);

            if (!result.Success)
                return BadRequest(result);

            return Ok(result);
        }

        // ---------------- GET PAYROLL BY ID ----------------
        [HttpGet("{id}")]
        public async Task<IActionResult> GetPayrollById(int id)
        {
            var result = await _payrollService.GetPayrollByIdAsync(id);

            if (!result.Success)
                return NotFound(result);

            return Ok(result);
        }

        // ---------------- GET USER PAYROLLS ----------------
        [HttpGet("user/{userId}")]
        public async Task<IActionResult> GetUserPayrolls(
            int userId,
            [FromQuery] int pageNumber = 1,
            [FromQuery] int pageSize = 10,
            [FromQuery] DateTime? fromMonth = null,
            [FromQuery] DateTime? toMonth = null,
            [FromQuery] decimal? minSalary = null,
            [FromQuery] decimal? maxSalary = null)
        {
            var result = await _payrollService.GetUserPayrollsAsync(userId, pageNumber, pageSize, fromMonth, toMonth, minSalary, maxSalary);
            if (result.Success)
                return Ok(result);
            return NotFound(result);
        }

        // ---------------- GET ALL PAYROLLS WITH FILTERS & PAGINATION ----------------
        [HttpGet]
        public async Task<IActionResult> GetAllPayrolls(
            [FromQuery] int pageNumber = 1,
            [FromQuery] int pageSize = 10,
            [FromQuery] int? userId = null,
            [FromQuery] DateTime? fromMonth = null,
            [FromQuery] DateTime? toMonth = null,
            [FromQuery] decimal? minSalary = null,
            [FromQuery] decimal? maxSalary = null)
        {
            var result = await _payrollService.GetAllPayrollsAsync(pageNumber, pageSize, userId, fromMonth, toMonth, minSalary, maxSalary);
            if (result.Success)
                return Ok(result);
            return NotFound(result);
        }
    }
}