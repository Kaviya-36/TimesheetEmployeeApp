using TimeSheetAppWeb.Model;
using TimeSheetAppWeb.Model.common;
using TimeSheetAppWeb.Model.DTOs;

namespace TimeSheetAppWeb.Interface
{
    public interface IAttendanceService
    {
        Task<ApiResponse<AttendanceResponse>> CheckInAsync(int userId);

        Task<ApiResponse<AttendanceResponse>> CheckOutAsync(int userId);
        Task<ApiResponse<AttendanceResponse>> GetTodayAsync(int userId);

        Task<ApiResponse<PagedResponse<AttendanceResponse>>> GetAllAttendanceAsync(int pageNumber, int pageSize);

        Task<ApiResponse<PagedResponse<AttendanceResponse>>> GetUserAttendanceAsync(int userId, int pageNumber, int pageSize);
        Task<IEnumerable<Attendance>> GetUserAttendanceEntitiesAsync(int userId);
    }
}