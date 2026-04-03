using TimeSheetAppWeb.Model;
using TimeSheetAppWeb.Model.common;
using TimeSheetAppWeb.Model.DTOs;

namespace TimeSheetAppWeb.Interface
{
    public interface ILeaveService
    {
        Task<ApiResponse<LeaveResponse>> ApplyLeaveAsync(int userId, LeaveCreateRequest request);
        Task<ApiResponse<bool>> ApproveOrRejectLeaveAsync(LeaveApprovalRequest request);
        Task<ApiResponse<IEnumerable<LeaveType>>> GetLeaveTypesAsync();
        Task<ApiResponse<bool>> DeleteLeaveAsync(int leaveId, int userId);
        Task<ApiResponse<PagedResponse<LeaveResponse>>> GetAllLeavesAsync(int pageNumber, int pageSize, string? search = null, string? status = null, string? sortDir = "desc");
        Task<ApiResponse<PagedResponse<LeaveResponse>>> GetUserLeavesAsync(int userId, int pageNumber, int pageSize, string? search = null, string? status = null, string? sortDir = "desc");
        Task<ApiResponse<List<object>>> GetLeaveBalanceAsync(int userId);
    }
}
