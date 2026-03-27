using Microsoft.AspNetCore.Mvc;
using TimeSheetAppWeb.Interface;

namespace TimeSheetAppWeb.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class AuditLogController : ControllerBase
    {
        private readonly IAuditService _auditService;
        public AuditLogController(IAuditService auditService) { _auditService = auditService; }

        [HttpGet]
        public async Task<IActionResult> GetAll() => Ok(await _auditService.GetAllAsync());

        [HttpGet("{id}")]
        public async Task<IActionResult> GetById(int id)
        {
            var result = await _auditService.GetByIdAsync(id);
            return result == null ? NotFound("Audit log not found") : Ok(result);
        }

        [HttpGet("table/{tableName}")]
        public async Task<IActionResult> GetByTable(string tableName) => Ok(await _auditService.GetByTableAsync(tableName));

        [HttpGet("action/{action}")]
        public async Task<IActionResult> GetByAction(string action) => Ok(await _auditService.GetByActionAsync(action));

        [HttpGet("user/{userId}")]
        public async Task<IActionResult> GetByUser(int userId) => Ok(await _auditService.GetByUserAsync(userId));

        // ── Unified paged + filtered + sorted endpoint ──────────────────────
        [HttpGet("paged")]
        public async Task<IActionResult> GetPaged(
            [FromQuery] int page = 1, [FromQuery] int pageSize = 10,
            [FromQuery] string? search = null, [FromQuery] string? action = null,
            [FromQuery] string? table = null,  [FromQuery] string? sortDir = "desc")
        {
            var result = await _auditService.GetPagedAsync(page, pageSize, search, action, table, sortDir);
            return Ok(result);
        }
    }
}
