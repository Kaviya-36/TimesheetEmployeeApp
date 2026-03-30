using TimeSheetAppWeb.Model;
using TimeSheetAppWeb.Model.common;
using TimeSheetAppWeb.Model.DTOs;

namespace TimeSheetAppWeb.Interface
{
    public interface ITimesheetService
    {
        Task<ApiResponse<TimesheetResponse>> CreateTimesheetFromAttendanceAsync(int userId, Attendance attendance);
        Task<ApiResponse<TimesheetResponse>> CreateManualTimesheetAsync(int userId, TimesheetCreateRequest request);
        Task<ApiResponse<TimesheetResponse>> CreateFromGridAsync(int userId, TimesheetGridRequest request);
        Task<ApiResponse<TimesheetWeeklyResponse>> SubmitWeeklyAsync(int userId, TimesheetWeeklyRequest request);
        Task<ApiResponse<TimesheetResponse>> UpdateTimesheetAsync(int timesheetId, TimesheetUpdateRequest request);
        Task<ApiResponse<bool>> DeleteTimesheetAsync(int timesheetId);

        Task<ApiResponse<PagedResponse<TimesheetResponse>>> GetUserTimesheetsAsync(int userId, PaginationParams paginationParams);
        Task<ApiResponse<PagedResponse<TimesheetResponse>>> GetAllTimesheetsAsync(PaginationParams paginationParams);
        Task<ApiResponse<bool>> ApproveOrRejectTimesheetAsync(TimesheetApprovalRequest request);
    }
}
