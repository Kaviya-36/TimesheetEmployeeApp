using TimeSheetAppWeb.Model;
using TimeSheetAppWeb.Model.common;
using TimeSheetAppWeb.Model.DTOs;

namespace TimeSheetAppWeb.Interface
{
    public interface ITimesheetService
    {
        Task<ApiResponse<TimesheetResponse>> CreateManualTimesheetAsync(int userId, TimesheetCreateRequest request);
        Task<ApiResponse<TimesheetWeeklyResponse>> SubmitWeeklyAsync(int userId, TimesheetWeeklyRequest request);
        Task<ApiResponse<TimesheetResponse>> UpdateTimesheetAsync(int timesheetId, TimesheetUpdateRequest request);
        Task<ApiResponse<bool>> DeleteTimesheetAsync(int timesheetId);
        Task<ApiResponse<PagedResponse<TimesheetResponse>>> GetUserTimesheetsAsync(int userId, PaginationParams paginationParams);
        Task<ApiResponse<PagedResponse<TimesheetResponse>>> GetAllTimesheetsAsync(PaginationParams paginationParams, int? callerId = null, string? callerRole = null);
        Task<ApiResponse<bool>> ApproveOrRejectTimesheetAsync(TimesheetApprovalRequest request);
    }
}
