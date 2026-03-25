using Microsoft.AspNetCore.Mvc;
using TimeSheetAppWeb.Interface;

namespace TimeSheetAppWeb.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class AuditLogController : ControllerBase
    {
        private readonly IAuditService _auditService;

        public AuditLogController(IAuditService auditService)
        {
            _auditService = auditService;
        }

        // ✅ Get all logs
        [HttpGet]
        public async Task<IActionResult> GetAll()
        {
            var result = await _auditService.GetAllAsync(); // ✅ FIXED
            return Ok(result);
        }

        // ✅ Get by Id
        [HttpGet("{id}")]
        public async Task<IActionResult> GetById(int id)
        {
            var result = await _auditService.GetByIdAsync(id); // ✅ FIXED

            if (result == null)
                return NotFound("Audit log not found");

            return Ok(result);
        }

        // ✅ Filter by Table
        [HttpGet("table/{tableName}")]
        public async Task<IActionResult> GetByTable(string tableName)
        {
            var result = await _auditService.GetByTableAsync(tableName); // ✅ FIXED
            return Ok(result);
        }

        // ✅ Filter by Action
        [HttpGet("action/{action}")]
        public async Task<IActionResult> GetByAction(string action)
        {
            var result = await _auditService.GetByActionAsync(action); // ✅ FIXED
            return Ok(result);
        }

        // ✅ Filter by User
        [HttpGet("user/{userId}")]
        public async Task<IActionResult> GetByUser(int userId)
        {
            var result = await _auditService.GetByUserAsync(userId); // ✅ FIXED
            return Ok(result);
        }

        // ✅ Pagination
        [HttpGet("paged")]
        public async Task<IActionResult> GetPaged(int page = 1, int pageSize = 10)
        {
            if (page <= 0 || pageSize <= 0)
                return BadRequest("Invalid pagination values");

            var result = await _auditService.GetPagedAsync(page, pageSize); // ✅ FIXED
            return Ok(result);
        }
    }
}