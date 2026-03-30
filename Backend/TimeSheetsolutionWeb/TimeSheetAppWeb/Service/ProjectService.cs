using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using TimeSheetAppWeb.Interface;
using TimeSheetAppWeb.Model;
using TimeSheetAppWeb.Model.DTOs;

namespace TimeSheetAppWeb.Services
{
    public class ProjectService : IProjectService
    {
        private readonly IRepository<int, Project> _projectRepository;
        private readonly IRepository<int, User> _userRepository;
        private readonly IRepository<int, ProjectAssignment> _assignmentRepository;
        private readonly ILogger<ProjectService> _logger;

        public ProjectService(
            IRepository<int, Project> projectRepository,
            IRepository<int, User> userRepository,
            IRepository<int, ProjectAssignment> assignmentRepository,
            ILogger<ProjectService> logger)
        {
            _projectRepository = projectRepository;
            _userRepository = userRepository;
            _assignmentRepository = assignmentRepository;
            _logger = logger;
        }

        // ---------------- CREATE PROJECT ----------------
        public async Task<ApiResponse<ProjectResponse>> CreateProjectAsync(ProjectCreateRequest request)
        {
            try
            {
                _logger.LogInformation("Creating new project: {ProjectName}", request.ProjectName);

                var today = DateTime.Today;

                // ---------------- DATE VALIDATION ----------------

                // Default StartDate if null
                var startDate = request.StartDate ?? DateTime.Now;

                // ❌ Prevent past start date
                if (startDate.Date < today)
                {
                    return new ApiResponse<ProjectResponse>
                    {
                        Success = false,
                        Message = "Start date cannot be in the past"
                    };
                }

                // ❌ End date before start date
                if (request.EndDate.HasValue && request.EndDate.Value < startDate)
                {
                    return new ApiResponse<ProjectResponse>
                    {
                        Success = false,
                        Message = "End date cannot be before start date"
                    };
                }

                // ❌ Optional: prevent past end date
                if (request.EndDate.HasValue && request.EndDate.Value.Date < today)
                {
                    return new ApiResponse<ProjectResponse>
                    {
                        Success = false,
                        Message = "End date cannot be in the past"
                    };
                }

                // ---------------- MANAGER VALIDATION ----------------

                User? manager = null;
                if (request.ManagerId.HasValue)
                {
                    manager = await _userRepository.GetByIdAsync(request.ManagerId.Value);

                    if (manager == null)
                    {
                        _logger.LogWarning("Manager with ID {ManagerId} not found", request.ManagerId);
                        return new ApiResponse<ProjectResponse>
                        {
                            Success = false,
                            Message = "Manager not found"
                        };
                    }

                    if (manager.Role != UserRole.Manager)
                    {
                        _logger.LogWarning("User ID {ManagerId} is not a Manager", request.ManagerId);
                        return new ApiResponse<ProjectResponse>
                        {
                            Success = false,
                            Message = "Only users with Manager role can be assigned as project manager"
                        };
                    }
                }

                // ---------------- CREATE PROJECT ----------------

                var project = new Project
                {
                    ProjectName = request.ProjectName,
                    Description = request.Description,
                    ManagerId = request.ManagerId,
                    StartDate = startDate,
                    EndDate = request.EndDate
                };

                await _projectRepository.AddAsync(project);

                // Auto-assign the manager to the project if one is set
                if (project.ManagerId.HasValue)
                {
                    var assignment = new ProjectAssignment
                    {
                        ProjectId    = project.Id,
                        UserId       = project.ManagerId.Value,
                        AssignedDate = DateTime.UtcNow
                    };
                    await _assignmentRepository.AddAsync(assignment);
                    _logger.LogInformation("Manager {ManagerId} auto-assigned to project {ProjectId}", project.ManagerId.Value, project.Id);
                }

                _logger.LogInformation("Project created successfully with ID {ProjectId}", project.Id);

                return new ApiResponse<ProjectResponse>
                {
                    Success = true,
                    Message = "Project created successfully",
                    Data = MapToDto(project, manager)
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating project: {ProjectName}", request.ProjectName);
                return new ApiResponse<ProjectResponse>
                {
                    Success = false,
                    Message = $"An error occurred while creating the project: {ex.Message}"
                };
            }
        }

        // ---------------- UPDATE PROJECT ----------------
        public async Task<ApiResponse<ProjectResponse>> UpdateProjectAsync(int projectId, ProjectUpdateRequest request)
        {
            try
            {
                _logger.LogInformation("Updating project ID {ProjectId}", projectId);

                var project = await _projectRepository.GetByIdAsync(projectId);
                if (project == null)
                {
                    _logger.LogWarning("Project with ID {ProjectId} not found", projectId);
                    return new ApiResponse<ProjectResponse> { Success = false, Message = "Project not found" };
                }

                // Update fields if provided
                if (!string.IsNullOrEmpty(request.ProjectName)) project.ProjectName = request.ProjectName;
                if (!string.IsNullOrEmpty(request.Description)) project.Description = request.Description;
                if (request.StartDate.HasValue) project.StartDate = request.StartDate.Value;
                if (request.EndDate.HasValue) project.EndDate = request.EndDate;

                User? manager = null;
                if (request.ManagerId.HasValue)
                {
                    manager = await _userRepository.GetByIdAsync(request.ManagerId.Value);
                    if (manager == null)
                    {
                        _logger.LogWarning("Manager with ID {ManagerId} not found", request.ManagerId);
                        return new ApiResponse<ProjectResponse> { Success = false, Message = "Manager not found" };
                    }

                    // Ensure the user is a Manager
                    if (manager.Role != UserRole.Manager)
                    {
                        _logger.LogWarning("User ID {ManagerId} is not a Manager", request.ManagerId);
                        return new ApiResponse<ProjectResponse>
                        {
                            Success = false,
                            Message = "Only users with Manager role can be assigned as project manager"
                        };
                    }

                    project.ManagerId = request.ManagerId;
                }

                await _projectRepository.UpdateAsync(projectId, project);
                _logger.LogInformation("Project ID {ProjectId} updated successfully", projectId);

                return new ApiResponse<ProjectResponse>
                {
                    Success = true,
                    Message = "Project updated successfully",
                    Data = MapToDto(project, manager)
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating project ID {ProjectId}", projectId);
                return new ApiResponse<ProjectResponse>
                {
                    Success = false,
                    Message = $"An error occurred while updating the project: {ex.Message}"
                };
            }
        }

        // ---------------- DELETE PROJECT ----------------
        public async Task<ApiResponse<bool>> DeleteProjectAsync(int projectId)
        {
            try
            {
                _logger.LogInformation("Deleting project ID {ProjectId}", projectId);

                var project = await _projectRepository.GetByIdAsync(projectId);
                if (project == null)
                {
                    _logger.LogWarning("Project with ID {ProjectId} not found", projectId);
                    return new ApiResponse<bool> { Success = false, Message = "Project not found", Data = false };
                }

                await _projectRepository.DeleteAsync(projectId);
                _logger.LogInformation("Project ID {ProjectId} deleted successfully", projectId);

                return new ApiResponse<bool>
                {
                    Success = true,
                    Message = "Project deleted successfully",
                    Data = true
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error deleting project ID {ProjectId}", projectId);
                return new ApiResponse<bool>
                {
                    Success = false,
                    Message = $"An error occurred while deleting the project: {ex.Message}",
                    Data = false
                };
            }
        }

        // ---------------- GET PROJECT BY ID ----------------
        public async Task<ApiResponse<ProjectResponse>> GetProjectByIdAsync(int projectId)
        {
            try
            {
                _logger.LogInformation("Fetching project by ID {ProjectId}", projectId);

                var project = await _projectRepository.GetByIdAsync(projectId);
                if (project == null)
                {
                    _logger.LogWarning("Project with ID {ProjectId} not found", projectId);
                    return new ApiResponse<ProjectResponse> { Success = false, Message = "Project not found" };
                }

                User? manager = null;
                if (project.ManagerId.HasValue)
                    manager = await _userRepository.GetByIdAsync(project.ManagerId.Value);

                return new ApiResponse<ProjectResponse>
                {
                    Success = true,
                    Data = MapToDto(project, manager)
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error fetching project ID {ProjectId}", projectId);
                return new ApiResponse<ProjectResponse>
                {
                    Success = false,
                    Message = $"An error occurred while fetching the project: {ex.Message}"
                };
            }
        }

        // ---------------- GET ALL PROJECTS ----------------
        public async Task<ApiResponse<IEnumerable<ProjectResponse>>> GetAllProjectsAsync(
            int pageNumber = 1,
            int pageSize = 10,
            int? managerId = null,
            DateTime? startFrom = null,
            DateTime? startTo = null,
            DateTime? endFrom = null,
            DateTime? endTo = null)
        {
            try
            {
                _logger.LogInformation("Fetching all projects with filters: ManagerId={ManagerId}, StartFrom={StartFrom}, StartTo={StartTo}, EndFrom={EndFrom}, EndTo={EndTo}",
                    managerId, startFrom, startTo, endFrom, endTo);

                var projects = await _projectRepository.GetAllAsync() ?? Enumerable.Empty<Project>();

                // Apply filters
                if (managerId.HasValue)
                    projects = projects.Where(p => p.ManagerId == managerId.Value);
                if (startFrom.HasValue)
                    projects = projects.Where(p => p.StartDate >= startFrom.Value);
                if (startTo.HasValue)
                    projects = projects.Where(p => p.StartDate <= startTo.Value);
                if (endFrom.HasValue)
                    projects = projects.Where(p => p.EndDate.HasValue && p.EndDate.Value >= endFrom.Value);
                if (endTo.HasValue)
                    projects = projects.Where(p => p.EndDate.HasValue && p.EndDate.Value <= endTo.Value);

                // Order by StartDate descending
                projects = projects.OrderByDescending(p => p.StartDate);

                // Pagination
                var pagedProjects = projects.Skip((pageNumber - 1) * pageSize).Take(pageSize);

                var result = new List<ProjectResponse>();
                foreach (var project in pagedProjects)
                {
                    User? manager = null;
                    if (project.ManagerId.HasValue)
                        manager = await _userRepository.GetByIdAsync(project.ManagerId.Value);

                    result.Add(MapToDto(project, manager));
                }

                _logger.LogInformation("Fetched {Count} projects", result.Count);
                return new ApiResponse<IEnumerable<ProjectResponse>>
                {
                    Success = true,
                    Data = result
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error fetching all projects");
                return new ApiResponse<IEnumerable<ProjectResponse>>
                {
                    Success = false,
                    Message = $"An error occurred while fetching all projects: {ex.Message}"
                };
            }
        }

        // ---------------- ASSIGN USER TO PROJECT ----------------
        public async Task<ApiResponse<ProjectAssignmentResponse>> AssignUserToProjectAsync(ProjectAssignRequest request)
        {
            try
            {
                _logger.LogInformation("Assigning user ID {UserId} to project ID {ProjectId}", request.UserId, request.ProjectId);
                var project = await _projectRepository.GetByIdAsync(request.ProjectId);
                if (project == null)
                {
                    _logger.LogWarning("Project with ID {ProjectId} not found", request.ProjectId);
                    return new ApiResponse<ProjectAssignmentResponse> { Success = false, Message = "Project not found" };
                }

                var user = await _userRepository.GetByIdAsync(request.UserId);
                if (user == null)
                {
                    _logger.LogWarning("User with ID {UserId} not found", request.UserId);
                    return new ApiResponse<ProjectAssignmentResponse> { Success = false, Message = "User not found" };
                }

                // Allow only Employees and Managers
                if (user.Role is not UserRole.Employee and not UserRole.Manager)
                {
                    _logger.LogWarning("User ID {UserId} with role {Role} cannot be assigned to project", request.UserId, user.Role);
                    return new ApiResponse<ProjectAssignmentResponse>
                    {
                        Success = false,
                        Message = "Only users with Employee or Manager role can be assigned to a project"
                    };
                }

                // Prevent duplicate assignment
                var existingAssignments = await _assignmentRepository.GetAllAsync();
                if (existingAssignments.Any(a => a.ProjectId == request.ProjectId && a.UserId == request.UserId))
                {
                    _logger.LogWarning("User ID {UserId} is already assigned to project ID {ProjectId}", request.UserId, request.ProjectId);
                    return new ApiResponse<ProjectAssignmentResponse>
                    {
                        Success = false,
                        Message = "User is already assigned to this project"
                    };
                }

                // Create assignment
                var assignment = new ProjectAssignment
                {
                    ProjectId = request.ProjectId,
                    UserId = request.UserId
                };

                await _assignmentRepository.AddAsync(assignment);
                _logger.LogInformation("User ID {UserId} assigned to project ID {ProjectId} successfully", request.UserId, request.ProjectId);

                return new ApiResponse<ProjectAssignmentResponse>
                {
                    Success = true,
                    Message = "User assigned successfully",
                    Data = new ProjectAssignmentResponse
                    {
                        Id = assignment.Id,
                        ProjectId = project.Id,
                        ProjectName = project.ProjectName,
                        UserId = user.Id,
                        UserName = user.Name,
                        UserEmail = user.Email
                    }
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error assigning user ID {UserId} to project ID {ProjectId}", request.UserId, request.ProjectId);
                return new ApiResponse<ProjectAssignmentResponse>
                {
                    Success = false,
                    Message = $"An error occurred while assigning user to project: {ex.Message}"
                };
            }
        }
        // ---------------- REMOVE USER FROM PROJECT ----------------
        public async Task<ApiResponse<bool>> RemoveUserFromProjectAsync(int assignmentId)
        {
            try
            {
                _logger.LogInformation("Removing assignment ID {AssignmentId}", assignmentId);

                var assignment = await _assignmentRepository.GetByIdAsync(assignmentId);
                if (assignment == null)
                {
                    _logger.LogWarning("Assignment with ID {AssignmentId} not found", assignmentId);
                    return new ApiResponse<bool> { Success = false, Message = "Assignment not found", Data = false };
                }

                await _assignmentRepository.DeleteAsync(assignmentId);
                _logger.LogInformation("Assignment ID {AssignmentId} removed successfully", assignmentId);

                return new ApiResponse<bool>
                {
                    Success = true,
                    Message = "User removed from project",
                    Data = true
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error removing assignment ID {AssignmentId}", assignmentId);
                return new ApiResponse<bool>
                {
                    Success = false,
                    Message = $"An error occurred while removing user from project: {ex.Message}",
                    Data = false
                };
            }
        }

        // ---------------- GET PROJECT ASSIGNMENTS ----------------
        public async Task<ApiResponse<IEnumerable<ProjectAssignmentResponse>>> GetProjectAssignmentsAsync(
            int projectId,
            int pageNumber = 1,
            int pageSize = 10)
        {
            try
            {
                _logger.LogInformation("Fetching assignments for project ID {ProjectId}", projectId);

                var assignments = (await _assignmentRepository.GetAllAsync())!
                    .Where(a => a.ProjectId == projectId)
                    .OrderBy(a => a.Id);

                var pagedAssignments = assignments.Skip((pageNumber - 1) * pageSize).Take(pageSize);

                var result = new List<ProjectAssignmentResponse>();
                foreach (var a in pagedAssignments)
                {
                    var user = await _userRepository.GetByIdAsync(a.UserId);
                    var project = await _projectRepository.GetByIdAsync(a.ProjectId);

                    result.Add(new ProjectAssignmentResponse
                    {
                        Id = a.Id,
                        ProjectId = project!.Id,
                        ProjectName = project.ProjectName,
                        UserId = user!.Id,
                        UserName = user.Name,
                        UserEmail = user.Email
                    });
                }

                _logger.LogInformation("Fetched {Count} assignments for project ID {ProjectId}", result.Count, projectId);
                return new ApiResponse<IEnumerable<ProjectAssignmentResponse>> { Success = true, Data = result };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error fetching assignments for project ID {ProjectId}", projectId);
                return new ApiResponse<IEnumerable<ProjectAssignmentResponse>>
                {
                    Success = false,
                    Message = $"An error occurred while fetching project assignments: {ex.Message}"
                };
            }
        }

        // ---------------- GET USER PROJECT ASSIGNMENTS ----------------
        public async Task<ApiResponse<IEnumerable<ProjectAssignmentResponse>>> GetUserProjectAssignmentsAsync(
            int userId,
            int pageNumber = 1,
            int pageSize = 10)
        {
            try
            {
                _logger.LogInformation("Fetching assignments for user ID {UserId}", userId);

                var allProjects = (await _projectRepository.GetAllAsync()) ?? Enumerable.Empty<Project>();
                var user = await _userRepository.GetByIdAsync(userId);
                if (user == null) return new ApiResponse<IEnumerable<ProjectAssignmentResponse>> { Success = false, Message = "User not found" };

                var result = new List<ProjectAssignmentResponse>();
                var addedProjectIds = new HashSet<int>();

                // 1. Projects assigned via ProjectAssignment table
                var assignments = (await _assignmentRepository.GetAllAsync())!
                    .Where(a => a.UserId == userId)
                    .OrderBy(a => a.Id)
                    .Skip((pageNumber - 1) * pageSize).Take(pageSize);

                foreach (var a in assignments)
                {
                    var project = await _projectRepository.GetByIdAsync(a.ProjectId);
                    if (project == null) continue;
                    addedProjectIds.Add(project.Id);
                    result.Add(new ProjectAssignmentResponse
                    {
                        Id = a.Id,
                        ProjectId = project.Id,
                        ProjectName = project.ProjectName,
                        UserId = user.Id,
                        UserName = user.Name,
                        UserEmail = user.Email
                    });
                }

                // 2. Projects where user is the manager (managerId field)
                foreach (var project in allProjects.Where(p => p.ManagerId == userId && !addedProjectIds.Contains(p.Id)))
                {
                    result.Add(new ProjectAssignmentResponse
                    {
                        Id = 0,  // synthetic — no assignment row
                        ProjectId = project.Id,
                        ProjectName = project.ProjectName,
                        UserId = user.Id,
                        UserName = user.Name,
                        UserEmail = user.Email
                    });
                }

                _logger.LogInformation("Fetched {Count} assignments for user ID {UserId}", result.Count, userId);
                return new ApiResponse<IEnumerable<ProjectAssignmentResponse>> { Success = true, Data = result };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error fetching assignments for user ID {UserId}", userId);
                return new ApiResponse<IEnumerable<ProjectAssignmentResponse>>
                {
                    Success = false,
                    Message = $"An error occurred while fetching user project assignments: {ex.Message}"
                };
            }
        }
        public async Task<ApiResponse<IEnumerable<ProjectResponse>>> GetMyProjectsAsync(int userId)
        {
            try
            {
                _logger.LogInformation("Fetching projects for user ID {UserId}", userId);

                var allProjects = (await _projectRepository.GetAllAsync()) ?? Enumerable.Empty<Project>();
                var assignments = (await _assignmentRepository.GetAllAsync())!.Where(a => a.UserId == userId);
                var addedIds    = new HashSet<int>();
                var result      = new List<ProjectResponse>();

                // Projects assigned via assignment table
                foreach (var a in assignments)
                {
                    var project = await _projectRepository.GetByIdAsync(a.ProjectId);
                    if (project == null) continue;
                    addedIds.Add(project.Id);
                    User? manager = project.ManagerId.HasValue ? await _userRepository.GetByIdAsync(project.ManagerId.Value) : null;
                    result.Add(MapToDto(project, manager));
                }

                // Projects where user is the manager
                foreach (var project in allProjects.Where(p => p.ManagerId == userId && !addedIds.Contains(p.Id)))
                {
                    var manager = await _userRepository.GetByIdAsync(userId);
                    result.Add(MapToDto(project, manager));
                }

                return new ApiResponse<IEnumerable<ProjectResponse>> { Success = true, Data = result };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error fetching projects for user ID {UserId}", userId);
                return new ApiResponse<IEnumerable<ProjectResponse>> { Success = false, Message = $"Error fetching user projects: {ex.Message}" };
            }
        }

        // ---------------- HELPER: MAP PROJECT TO DTO ----------------
        private ProjectResponse MapToDto(Project project, User? manager)
        {
            return new ProjectResponse
            {
                Id = project.Id,
                ProjectName = project.ProjectName,
                Description = project.Description,
                ManagerId = project.ManagerId,
                ManagerName = manager?.Name,
                StartDate = project.StartDate,
                EndDate = project.EndDate
            };
        }
    }
}