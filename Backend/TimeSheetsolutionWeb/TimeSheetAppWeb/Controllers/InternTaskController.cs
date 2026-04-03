using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;
using TimeSheetAppWeb.Interface;
using TimeSheetAppWeb.Model.DTOs;
using TimeSheetAppWeb.Services;

namespace TimeSheetAppWeb.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    [Authorize] // Require JWT authentication
    public class InternTaskController : ControllerBase
    {
        private readonly IInternTaskService _taskService;

        public InternTaskController(IInternTaskService taskService)
        {
            _taskService = taskService;
        }

        // ---------------- CREATE TASK ----------------
        [HttpPost("create")]
        [Authorize(Roles = "Mentor,Manager")]
        public async Task<IActionResult> CreateTask([FromBody] InternTaskCreateRequest request)
        {
            var role = User.FindFirst(ClaimTypes.Role)?.Value ?? "";
            var response = await _taskService.CreateTaskAsync(request, role);
            return response.Success ? Ok(response) : BadRequest(response);
        }

        // ---------------- UPDATE TASK ----------------
        [HttpPut("update/{taskId}")]
        [Authorize(Roles = "Mentor,Manager")]
        public async Task<IActionResult> UpdateTask(int taskId, [FromBody] InternTaskUpdateRequest request)
        {
            var role = User.FindFirst(ClaimTypes.Role)?.Value ?? "";
            var response = await _taskService.UpdateTaskAsync(taskId, request, role);
            return response.Success ? Ok(response) : BadRequest(response);
        }

        // ---------------- GET INTERN TASKS ----------------
        [HttpGet("intern/{internId}")]
        [Authorize(Roles = "Mentor,HR,Manager,Admin,Intern")]
        public async Task<IActionResult> GetInternTasks(int internId, int pageNumber = 1, int pageSize = 10)
        {
            var result = await _taskService.GetInternTasksAsync(internId, pageNumber, pageSize);
            return Ok(result);
        }

        // ---------------- DELETE TASK ----------------
        [HttpDelete("delete/{taskId}")]
        [Authorize(Roles = "Manager,Mentor")]
        public async Task<IActionResult> DeleteTask(int taskId)
        {
            var role = User.FindFirst(ClaimTypes.Role)?.Value ?? "";
            var response = await _taskService.DeleteTaskAsync(taskId, role);
            return response.Success ? Ok(response) : BadRequest(response);
        }

        // ---------------- UPDATE STATUS (Intern self-update) ----------------
        [HttpPatch("status/{taskId}")]
        [Authorize(Roles = "Intern")]
        public async Task<IActionResult> UpdateStatus(int taskId, [FromBody] UpdateTaskStatusRequest request)
        {
            var response = await _taskService.UpdateTaskAsync(
                taskId,
                new InternTaskUpdateRequest { Status = request.Status },
                "Mentor"   // pass Mentor to bypass role check inside service
            );
            return response.Success ? Ok(response) : BadRequest(response);
        }
    }
}

// DTO for status-only update
public record UpdateTaskStatusRequest(TimeSheetAppWeb.Model.TaskStatus Status);