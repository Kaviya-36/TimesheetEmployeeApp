using TimeSheetAppWeb.Model;

namespace TimeSheetAppWeb.Interface
{
    public interface IAuditService
    {
        Task<IEnumerable<AuditLog>> GetAllAsync();

        Task<AuditLog?> GetByIdAsync(int id);

        Task<IEnumerable<AuditLog>> GetByTableAsync(string tableName);

        Task<IEnumerable<AuditLog>> GetByActionAsync(string action);

        Task<IEnumerable<AuditLog>> GetByUserAsync(int userId);

        Task<IEnumerable<AuditLog>> GetPagedAsync(int page, int pageSize);
    }
}