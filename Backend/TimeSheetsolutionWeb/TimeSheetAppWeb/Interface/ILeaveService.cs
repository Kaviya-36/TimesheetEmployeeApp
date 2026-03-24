using TimeSheetAppWeb.Model;
using TimeSheetAppWeb.Model.common;
using TimeSheetAppWeb.Model.DTOs;

namespace TimeSheetAppWeb.Interface
{
    public interface ILeaveService
    {
        Task<ApiResponse<LeaveResponse>> ApplyLeaveAsync(int userId, LeaveCreateRequest request);
        Task<ApiResponse<bool>> ApproveOrRejectLeaveAsync(LeaveApprovalRequest request);

        Task<ApiResponse<PagedResponse<LeaveResponse>>> GetAllLeavesAsync(int pageNumber, int pageSize);

        Task<ApiResponse<PagedResponse<LeaveResponse>>> GetUserLeavesAsync(int userId, int pageNumber, int pageSize);
        Task<ApiResponse<IEnumerable<LeaveType>>> GetLeaveTypesAsync();
    }
}
