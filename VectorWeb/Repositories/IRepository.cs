using System.Linq.Expressions;

namespace VectorWeb.Repositories
{
    public interface IRepository<T> where T : class
    {
        IQueryable<T> GetQueryable(string includeProperties = "");
        Task<IEnumerable<T>> GetAllAsync();
        Task<T?> GetByIdAsync(int id);
        Task<IEnumerable<T>> FindAsync(Expression<Func<T, bool>> predicate);
        Task AddAsync(T entity);
        Task UpdateAsync(T entity);
        Task RemoveAsync(T entity);
        Task DeleteAsync(int id);
        Task<bool> ExistsAsync(int id);
    }
}
