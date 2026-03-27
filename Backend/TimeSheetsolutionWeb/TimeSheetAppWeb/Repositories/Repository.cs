
using TimeSheetAppWeb.Interface;
using Microsoft.EntityFrameworkCore;
using TimeSheetAppWeb.Contexts;

namespace TimeSheetAppWeb.Repositories
{
    public class Repository<K, T> : IRepository<K, T> where T : class
    {
        private readonly TimeSheetContext _context;

        public Repository(TimeSheetContext context)
        {
            _context = context;
        }
        public Task<IQueryable<T>> GetQueryableAsync()
        {
            return Task.FromResult(_context.Set<T>().AsQueryable());
        }

        public async Task<T?> AddAsync(T entity)
        {
            _context.Add(entity);
            await _context.SaveChangesAsync();
            return entity;
        }

        public async Task<T> DeleteAsync(K id)
        {
            var entity = await GetByIdAsync(id);
            if (entity == null)
            {
                throw new KeyNotFoundException($"Entity with id {id} not found.");
            }
            _context.Remove(entity);
            await _context.SaveChangesAsync();
            return entity;
        }

        public async Task<IEnumerable<T>?> GetAllAsync()
        {
            var entities = _context.Set<T>();
            if (entities == null)
                throw new InvalidOperationException("No entities found.");
            return await entities.AsNoTracking().ToListAsync();
        }

        public async Task<T?> GetByIdAsync(K id)
        {
            var entity = await _context.Set<T>().FindAsync(id);
            if (entity == null)
                throw new KeyNotFoundException($"Entity with id {id} not found.");
            return entity;
        }

        public async Task<T?> UpdateAsync(K key, T entity)
        {
            var existing = await _context.Set<T>().FindAsync(key);
            if (existing == null)
                throw new KeyNotFoundException($"Entity with id {key} not found.");

            _context.Entry(existing).CurrentValues.SetValues(entity);
            await _context.SaveChangesAsync();
            return existing;
        }
    }
}
