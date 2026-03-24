namespace TimeSheetAppWeb.Interface
{
    public interface IRepository <K,T> where T : class
    {
        Task<IEnumerable<T>?> GetAllAsync();
        Task<T?> GetByIdAsync(K id);
        Task<T?> AddAsync(T entity);
        Task<T?> UpdateAsync(K key,T entity);
        Task<T> DeleteAsync(K id);
    }
}
