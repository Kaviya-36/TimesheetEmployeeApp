using Microsoft.EntityFrameworkCore;
using TimeSheetAppWeb.Interface;
using TimeSheetAppWeb.Model;

namespace TimeSheetAppWeb.Services
{
    public class AuditService:IAuditService
    {
        private readonly IRepository<int, AuditLog> _repo;

        public AuditService(IRepository<int, AuditLog> repo)
        {
            _repo = repo;
        }


        public async Task<IEnumerable<AuditLog>> GetAllAsync()
        {
            var data = await _repo.GetAllAsync();
            return data?.OrderByDescending(x => x.ChangedAt) ?? Enumerable.Empty<AuditLog>();
        }


        public async Task<AuditLog?> GetByIdAsync(int id)
        {
            return await _repo.GetByIdAsync(id);
        }


        public async Task<IEnumerable<AuditLog>> GetByTableAsync(string tableName)
        {
            var data = await _repo.GetAllAsync();

            return data?
                .Where(x => x.TableName.ToLower() == tableName.ToLower())
                .OrderByDescending(x => x.ChangedAt)
                ?? Enumerable.Empty<AuditLog>();
        }


        public async Task<IEnumerable<AuditLog>> GetByActionAsync(string action)
        {
            var data = await _repo.GetAllAsync();

            return data?
                .Where(x => x.Action.ToLower() == action.ToLower())
                .OrderByDescending(x => x.ChangedAt)
                ?? Enumerable.Empty<AuditLog>();
        }


        public async Task<IEnumerable<AuditLog>> GetByUserAsync(int userId)
        {
            var data = await _repo.GetAllAsync();

            return data?
                .Where(x => x.UserId == userId)
                .OrderByDescending(x => x.ChangedAt)
                ?? Enumerable.Empty<AuditLog>();
        }


        public async Task<IEnumerable<AuditLog>> GetPagedAsync(int page, int pageSize)
        {
            var data = await _repo.GetAllAsync();

            return data?
                .OrderByDescending(x => x.ChangedAt)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                ?? Enumerable.Empty<AuditLog>();
        }
    }
}