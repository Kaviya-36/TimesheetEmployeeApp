using TimeSheetAppWeb.Interface;
using TimeSheetAppWeb.Model;
using TimeSheetAppWeb.Model.common;

namespace TimeSheetAppWeb.Services
{
    public class AuditService : IAuditService
    {
        private readonly IRepository<int, AuditLog> _repo;
        public AuditService(IRepository<int, AuditLog> repo) { _repo = repo; }

        public async Task<IEnumerable<AuditLog>> GetAllAsync() =>
            (await _repo.GetAllAsync())?.OrderByDescending(x => x.ChangedAt) ?? Enumerable.Empty<AuditLog>();

        public async Task<AuditLog?> GetByIdAsync(int id) => await _repo.GetByIdAsync(id);

        public async Task<IEnumerable<AuditLog>> GetByTableAsync(string tableName) =>
            (await _repo.GetAllAsync())?.Where(x => x.TableName.ToLower() == tableName.ToLower()).OrderByDescending(x => x.ChangedAt) ?? Enumerable.Empty<AuditLog>();

        public async Task<IEnumerable<AuditLog>> GetByActionAsync(string action) =>
            (await _repo.GetAllAsync())?.Where(x => x.Action.ToLower() == action.ToLower()).OrderByDescending(x => x.ChangedAt) ?? Enumerable.Empty<AuditLog>();

        public async Task<IEnumerable<AuditLog>> GetByUserAsync(int userId) =>
            (await _repo.GetAllAsync())?.Where(x => x.UserId == userId).OrderByDescending(x => x.ChangedAt) ?? Enumerable.Empty<AuditLog>();

        public async Task<PagedResponse<AuditLog>> GetPagedAsync(int page, int pageSize, string? search = null, string? action = null, string? table = null, string? sortDir = "desc")
        {
            var data = await _repo.GetAllAsync() ?? Enumerable.Empty<AuditLog>();

            if (!string.IsNullOrWhiteSpace(search))
            {
                var q = search.ToLower();
                data = data.Where(x => x.TableName.ToLower().Contains(q) || x.Action.ToLower().Contains(q) || (x.KeyValues ?? "").ToLower().Contains(q));
            }
            if (!string.IsNullOrWhiteSpace(action))
                data = data.Where(x => x.Action.ToUpper() == action.ToUpper());
            if (!string.IsNullOrWhiteSpace(table))
                data = data.Where(x => x.TableName.ToLower() == table.ToLower());

            data = sortDir?.ToLower() == "asc" ? data.OrderBy(x => x.ChangedAt) : data.OrderByDescending(x => x.ChangedAt);

            var total = data.Count();
            var paged = data.Skip((page - 1) * pageSize).Take(pageSize).ToList();
            return new PagedResponse<AuditLog>(paged, total, page, pageSize);
        }
    }
}