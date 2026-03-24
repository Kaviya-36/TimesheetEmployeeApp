using TimeSheetAppWeb.Model.common;
using TimeSheetAppWeb.Model.DTOs;

namespace TimeSheetAppWeb.Interface
{
    public interface IInternTaskService
    {
        // Add role parameter for role-based checks
        Task<ApiResponse<InternTaskResponse>> CreateTaskAsync(InternTaskCreateRequest request, string userRole);
        Task<ApiResponse<InternTaskResponse>> UpdateTaskAsync(int taskId, InternTaskUpdateRequest request, string userRole);
        Task<ApiResponse<PagedResponse<InternTaskResponse>>> GetInternTasksAsync(int internId, int pageNumber, int pageSize);
        Task<ApiResponse<bool>> DeleteTaskAsync(int taskId, string userRole);
    }
}