using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TimeSheetAppWeb.Interface;
using TimeSheetAppWeb.Model.DTOs;

namespace TimeSheetAppWeb.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    [Authorize] // JWT required for all actions
    public class ProjectController : ControllerBase
    {
        private readonly IProjectService _projectService;

        public ProjectController(IProjectService projectService)
        {
            _projectService = projectService;
        }

        // ---------------- CREATE PROJECT ----------------
        [HttpPost]
        [Authorize(Roles = "Admin,HR,Manager")]
        public async Task<IActionResult> CreateProject([FromBody] ProjectCreateRequest request)
        {
            try
            {
                var result = await _projectService.CreateProjectAsync(request);
                return StatusCode(result.Success ? 200 : 400, result);
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { Success = false, Message = $"Error creating project: {ex.Message}" });
            }
        }

        // ---------------- UPDATE PROJECT ----------------
        [HttpPut("{projectId}")]
        [Authorize(Roles = "Admin,HR,Manager")]
        public async Task<IActionResult> UpdateProject(int projectId, [FromBody] ProjectUpdateRequest request)
        {
            try
            {
                var result = await _projectService.UpdateProjectAsync(projectId, request);
                return StatusCode(result.Success ? 200 : 400, result);
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { Success = false, Message = $"Error updating project: {ex.Message}" });
            }
        }

        // ---------------- DELETE PROJECT ----------------
        [HttpDelete("{projectId}")]
        [Authorize(Roles = "Admin,HR,Manager")]
        public async Task<IActionResult> DeleteProject(int projectId)
        {
            try
            {
                var result = await _projectService.DeleteProjectAsync(projectId);
                return StatusCode(result.Success ? 200 : 400, result);
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { Success = false, Message = $"Error deleting project: {ex.Message}" });
            }
        }

        // ---------------- GET PROJECT BY ID ----------------
        [HttpGet("{projectId}")]
        public async Task<IActionResult> GetProjectById(int projectId)
        {
            try
            {
                var result = await _projectService.GetProjectByIdAsync(projectId);
                return StatusCode(result.Success ? 200 : 404, result);
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { Success = false, Message = $"Error fetching project: {ex.Message}" });
            }
        }

        // ---------------- GET ALL PROJECTS WITH PAGINATION ----------------
        [HttpGet]
        [Authorize(Roles = "Admin,HR,Manager,Mentor")]
        public async Task<IActionResult> GetAllProjects([FromQuery] int pageNumber = 1, [FromQuery] int pageSize = 10)
        {
            try
            {
                var result = await _projectService.GetAllProjectsAsync(pageNumber, pageSize);
                return Ok(result);
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { Success = false, Message = $"Error fetching projects: {ex.Message}" });
            }
        }

        // ---------------- ASSIGN USER TO PROJECT ----------------
        [HttpPost("assign")]
        [Authorize(Roles = "Admin,HR,Manager")]
        public async Task<IActionResult> AssignUserToProject([FromBody] ProjectAssignRequest request)
        {
            try
            {
                var result = await _projectService.AssignUserToProjectAsync(request);
                return StatusCode(result.Success ? 200 : 400, result);
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { Success = false, Message = $"Error assigning user to project: {ex.Message}" });
            }
        }


        // ---------------- REMOVE USER FROM PROJECT ----------------
        [HttpDelete("assignment/{assignmentId}")]
        [Authorize(Roles = "Admin,HR,Manager")]
        public async Task<IActionResult> RemoveUserFromProject(int assignmentId)
        {
            try
            {
                var result = await _projectService.RemoveUserFromProjectAsync(assignmentId);
                return StatusCode(result.Success ? 200 : 400, result);
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { Success = false, Message = $"Error removing user from project: {ex.Message}" });
            }
        }

        [HttpGet("my")]
        [Authorize] // any logged-in user
        public async Task<IActionResult> GetMyProjects()
        {
            try
            {
                var userIdClaim = User.FindFirst("id")?.Value;

                if (string.IsNullOrEmpty(userIdClaim))
                    return Unauthorized();

                int userId = int.Parse(userIdClaim);

                var result = await _projectService.GetMyProjectsAsync(userId);

                return Ok(result);
            }
            catch (Exception ex)
            {
                return StatusCode(500, new
                {
                    Success = false,
                    Message = $"Error fetching user projects: {ex.Message}"
                });
            }
        }

        // ---------------- GET PROJECT ASSIGNMENTS WITH PAGINATION ----------------
        [HttpGet("{projectId}/assignments")]
        [Authorize(Roles = "Employee,Admin,HR,Manager")]
        public async Task<IActionResult> GetProjectAssignments(int projectId, [FromQuery] int pageNumber = 1, [FromQuery] int pageSize = 10)
        {
            try
            {
                var result = await _projectService.GetProjectAssignmentsAsync(projectId, pageNumber, pageSize);
                return Ok(result);
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { Success = false, Message = $"Error fetching project assignments: {ex.Message}" });
            }
        }

        // ---------------- GET USER PROJECT ASSIGNMENTS WITH PAGINATION ----------------
        [HttpGet("user/{userId}/assignments")]
        public async Task<IActionResult> GetUserProjectAssignments(int userId, [FromQuery] int pageNumber = 1, [FromQuery] int pageSize = 10)
        {
            try
            {
                var result = await _projectService.GetUserProjectAssignmentsAsync(userId, pageNumber, pageSize);
                return Ok(result);
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { Success = false, Message = $"Error fetching user project assignments: {ex.Message}" });
            }
        }
    }
}