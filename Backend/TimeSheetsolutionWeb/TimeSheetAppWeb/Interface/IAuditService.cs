using TimeSheetAppWeb.Model;
using TimeSheetAppWeb.Model.common;

namespace TimeSheetAppWeb.Interface
{
    public interface IAuditService
    {
        Task<IEnumerable<AuditLog>> GetAllAsync();
        Task<AuditLog?> GetByIdAsync(int id);
        Task<IEnumerable<AuditLog>> GetByTableAsync(string tableName);
        Task<IEnumerable<AuditLog>> GetByActionAsync(string action);
        Task<IEnumerable<AuditLog>> GetByUserAsync(int userId);
        Task<PagedResponse<AuditLog>> GetPagedAsync(int page, int pageSize, string? search = null, string? action = null, string? table = null, string? sortDir = "desc");
    }
}