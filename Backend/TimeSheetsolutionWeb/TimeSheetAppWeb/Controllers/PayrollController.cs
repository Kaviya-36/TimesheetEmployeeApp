using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TimeSheetAppWeb.Interface;
using TimeSheetAppWeb.Model.DTOs;

namespace TimeSheetAppWeb.Controllers
{
    [Route("api/payroll")] // ✅ lowercase FIX
    [ApiController]
    [Authorize]
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

            return result.Success ? Ok(result) : BadRequest(result);
        }

        // ---------------- GET PAYROLL BY ID ----------------
        [HttpGet("{id:int}")]
        public async Task<IActionResult> GetPayrollById(int id)
        {
            var result = await _payrollService.GetPayrollByIdAsync(id);

            return result.Success ? Ok(result) : NotFound(result);
        }

        // ---------------- GET USER PAYROLLS ----------------
        [HttpGet("user/{userId:int}")]
        public async Task<IActionResult> GetUserPayrolls(
            int userId,
            [FromQuery] int pageNumber = 1,
            [FromQuery] int pageSize = 10,
            [FromQuery] DateTime? fromMonth = null,
            [FromQuery] DateTime? toMonth = null,
            [FromQuery] decimal? minSalary = null,
            [FromQuery] decimal? maxSalary = null)
        {
            var result = await _payrollService.GetUserPayrollsAsync(
                userId, pageNumber, pageSize, fromMonth, toMonth, minSalary, maxSalary);

            // ✅ Always return 200 for lists
            return Ok(result);
        }

        // ---------------- GET ALL PAYROLLS ----------------
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
            var result = await _payrollService.GetAllPayrollsAsync(
                pageNumber, pageSize, userId, fromMonth, toMonth, minSalary, maxSalary);

            // ✅ Return OK even if empty
            return Ok(result);
        }
    }
}