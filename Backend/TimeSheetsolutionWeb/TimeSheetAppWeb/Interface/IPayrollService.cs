using TimeSheetAppWeb.Model.DTOs;

namespace TimeSheetAppWeb.Interface
{
    public interface IPayrollService
    {
        Task<ApiResponse<PayrollResponse>> CreatePayrollAsync(PayrollCreateRequest request);
        Task<ApiResponse<PayrollResponse>> GetPayrollByIdAsync(int payrollId);
        Task<ApiResponse<IEnumerable<PayrollResponse>>> GetUserPayrollsAsync(
            int userId,
            int pageNumber = 1,
            int pageSize = 10,
            DateTime? fromMonth = null,
            DateTime? toMonth = null,
            decimal? minSalary = null,
            decimal? maxSalary = null);

        // Updated to support pagination and filters
        Task<ApiResponse<IEnumerable<PayrollResponse>>> GetAllPayrollsAsync(
            int pageNumber = 1,
            int pageSize = 10,
            int? userId = null,
            DateTime? fromMonth = null,
            DateTime? toMonth = null,
            decimal? minSalary = null,
            decimal? maxSalary = null);
    }
}
