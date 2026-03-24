using TimeSheetAppWeb.Model.DTOs;
using TimeSheetAppWeb.Model.common;

namespace TimeSheetAppWeb.Interface
{
    public interface IInternDetailsService
    {
        Task<ApiResponse<PagedResponse<InternDetailsDto>>> GetAllAsync(
            int pageNumber,
            int pageSize,
            string? userName,
            string? mentorName);

        Task<ApiResponse<InternDetailsDto>> GetByIdAsync(int id);

        Task<ApiResponse<InternDetailsDto>> CreateAsync(InternDetailsCreateDto dto);

        Task<ApiResponse<InternDetailsDto>> UpdateAsync(int id, InternDetailsCreateDto dto);

        Task<ApiResponse<bool>> DeleteAsync(int id);
    }
}