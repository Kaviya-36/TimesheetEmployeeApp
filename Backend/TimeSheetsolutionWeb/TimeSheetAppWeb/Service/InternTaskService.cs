using Microsoft.Extensions.Logging;
using TimeSheetAppWeb.Interface;
using TimeSheetAppWeb.Model;
using TimeSheetAppWeb.Model.common;
using TimeSheetAppWeb.Model.DTOs;
using TaskStatus = TimeSheetAppWeb.Model.TaskStatus;

namespace TimeSheetAppWeb.Services
{
    public class InternTaskService : IInternTaskService
    {
        private readonly IRepository<int, InternTask> _taskRepository;
        private readonly IRepository<int, User> _userRepository;
        private readonly ILogger<InternTaskService> _logger;

        public InternTaskService(IRepository<int, InternTask> taskRepository,
                                 IRepository<int, User> userRepository,
                                 ILogger<InternTaskService> logger)
        {
            _taskRepository = taskRepository;
            _userRepository = userRepository;
            _logger = logger;
        }

        // ---------------- CREATE TASK ----------------
        public async Task<ApiResponse<InternTaskResponse>> CreateTaskAsync(InternTaskCreateRequest request, string userRole)
        {
            try
            {
                _logger.LogInformation("CreateTask requested by Role={UserRole} for InternId={InternId}", userRole, request.InternId);

                if (userRole != "Mentor")
                    return new ApiResponse<InternTaskResponse>
                    {
                        Success = false,
                        Message = "Only mentors can create tasks."
                    };

                var intern = await _userRepository.GetByIdAsync(request.InternId);
                if (intern == null)
                {
                    _logger.LogWarning("Intern not found: InternId={InternId}", request.InternId);
                    return new ApiResponse<InternTaskResponse>
                    {
                        Success = false,
                        Message = "Intern not found"
                    };
                }

                if (intern.Role != UserRole.Intern)
                {
                    _logger.LogWarning("User {UserId} is not an Intern", request.InternId);
                    return new ApiResponse<InternTaskResponse>
                    {
                        Success = false,
                        Message = "Only users with role 'Intern' can be assigned tasks."
                    };
                }

                var task = new InternTask
                {
                    InternId = request.InternId,
                    Title = request.Title,
                    Description = request.Description,
                    AssignedDate = DateTime.Now,
                    DueDate = request.DueDate,
                    Status = TaskStatus.Pending
                };

                await _taskRepository.AddAsync(task);
                _logger.LogInformation("Task created successfully: TaskId={TaskId} for InternId={InternId}", task.Id, request.InternId);

                return new ApiResponse<InternTaskResponse>
                {
                    Success = true,
                    Message = "Task created successfully",
                    Data = MapToDto(task, intern)
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating task for InternId={InternId}", request.InternId);
                return new ApiResponse<InternTaskResponse>
                {
                    Success = false,
                    Message = $"Error creating task: {ex.Message}"
                };
            }
        }

        // ---------------- UPDATE TASK ----------------
        public async Task<ApiResponse<InternTaskResponse>> UpdateTaskAsync(int taskId, InternTaskUpdateRequest request, string userRole)
        {
            try
            {
                _logger.LogInformation("UpdateTask requested by Role={UserRole} for TaskId={TaskId}", userRole, taskId);

                if (userRole != "Mentor")
                    return new ApiResponse<InternTaskResponse>
                    {
                        Success = false,
                        Message = "Only mentors can update tasks."
                    };

                var task = await _taskRepository.GetByIdAsync(taskId);
                if (task == null)
                {
                    _logger.LogWarning("Task not found: TaskId={TaskId}", taskId);
                    return new ApiResponse<InternTaskResponse>
                    {
                        Success = false,
                        Message = "Task not found"
                    };
                }

                var intern = await _userRepository.GetByIdAsync(task.InternId);
                if (intern == null)
                {
                    _logger.LogWarning("Intern not found: InternId={InternId}", task.InternId);
                    return new ApiResponse<InternTaskResponse>
                    {
                        Success = false,
                        Message = "Intern not found"
                    };
                }

                if (!string.IsNullOrEmpty(request.Title)) task.Title = request.Title;
                if (!string.IsNullOrEmpty(request.Description)) task.Description = request.Description;
                if (request.DueDate.HasValue) task.DueDate = request.DueDate.Value;
                task.Status = request.Status;

                await _taskRepository.UpdateAsync(task.Id, task);
                _logger.LogInformation("Task updated successfully: TaskId={TaskId}", task.Id);

                return new ApiResponse<InternTaskResponse>
                {
                    Success = true,
                    Message = "Task updated successfully",
                    Data = MapToDto(task, intern)
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating task: TaskId={TaskId}", taskId);
                return new ApiResponse<InternTaskResponse>
                {
                    Success = false,
                    Message = $"Error updating task: {ex.Message}"
                };
            }
        }

        // ---------------- GET INTERN TASKS ----------------
        public async Task<ApiResponse<PagedResponse<InternTaskResponse>>> GetInternTasksAsync(int internId, int pageNumber, int pageSize)
        {
            try
            {
                _logger.LogInformation("Fetching tasks for InternId={InternId}, Page={PageNumber}, PageSize={PageSize}", internId, pageNumber, pageSize);

                var allTasks = await _taskRepository.GetAllAsync() ?? Enumerable.Empty<InternTask>();
                var internTasks = allTasks
                    .Where(t => t != null && t.InternId == internId)
                    .OrderByDescending(t => t!.AssignedDate);

                var totalRecords = internTasks.Count();
                var pagedTasks = internTasks.Skip((pageNumber - 1) * pageSize).Take(pageSize);

                var result = new List<InternTaskResponse>();
                foreach (var task in pagedTasks)
                {
                    var intern = await _userRepository.GetByIdAsync(task.InternId);
                    if (intern != null)
                        result.Add(MapToDto(task, intern));
                }

                _logger.LogInformation("Fetched {Count} tasks for InternId={InternId}", result.Count, internId);

                return new ApiResponse<PagedResponse<InternTaskResponse>>
                {
                    Success = true,
                    Data = new PagedResponse<InternTaskResponse>(result, totalRecords, pageNumber, pageSize)
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error fetching tasks for InternId={InternId}", internId);
                return new ApiResponse<PagedResponse<InternTaskResponse>>
                {
                    Success = false,
                    Message = $"Error fetching tasks: {ex.Message}"
                };
            }
        }

        // ---------------- DELETE TASK ----------------
        public async Task<ApiResponse<bool>> DeleteTaskAsync(int taskId, string userRole)
        {
            try
            {
                _logger.LogInformation("DeleteTask requested by Role={UserRole} for TaskId={TaskId}", userRole, taskId);

                if (userRole != "HR" && userRole != "Manager" && userRole != "Admin")
                    return new ApiResponse<bool>
                    {
                        Success = false,
                        Message = "Only HR, Manager, or Admin can delete tasks."
                    };

                var task = await _taskRepository.GetByIdAsync(taskId);
                if (task == null)
                {
                    _logger.LogWarning("Task not found: TaskId={TaskId}", taskId);
                    return new ApiResponse<bool>
                    {
                        Success = false,
                        Message = "Task not found"
                    };
                }

                await _taskRepository.DeleteAsync(task.Id);
                _logger.LogInformation("Task deleted successfully: TaskId={TaskId}", task.Id);

                return new ApiResponse<bool>
                {
                    Success = true,
                    Message = "Task deleted successfully",
                    Data = true
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error deleting task: TaskId={TaskId}", taskId);
                return new ApiResponse<bool>
                {
                    Success = false,
                    Message = $"Error deleting task: {ex.Message}"
                };
            }
        }

        // ---------------- HELPER: DTO MAPPING ----------------
        private InternTaskResponse MapToDto(InternTask task, User intern)
        {
            return new InternTaskResponse
            {
                Id = task.Id,
                InternName = intern.Name,
                Title = task.Title ?? string.Empty,
                Description = task.Description,
                DueDate = task.DueDate ?? DateTime.MinValue
            };
        }
    }
}