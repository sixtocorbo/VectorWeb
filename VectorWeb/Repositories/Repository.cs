using Microsoft.EntityFrameworkCore;
using System.Linq.Expressions;
using VectorWeb.Models; // Asegúrate que este namespace coincida con tu carpeta Models

namespace VectorWeb.Repositories
{
    public class Repository<T> : IRepository<T> where T : class
    {
        private readonly SecretariaDbContext _context;
        private readonly DbSet<T> _dbSet;

        public Repository(SecretariaDbContext context)
        {
            _context = context;
            _dbSet = context.Set<T>();
        }


        public IQueryable<T> GetQueryable(string includeProperties = "")
        {
            IQueryable<T> query = _dbSet;

            foreach (var includeProperty in includeProperties.Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
            {
                query = query.Include(includeProperty);
            }

            return query.AsNoTracking();
        }

        public async Task<IEnumerable<T>> GetAllAsync()
        {
            return await _dbSet.ToListAsync();
        }

        public async Task<T?> GetByIdAsync(int id)
        {
            return await _dbSet.FindAsync(id);
        }

        public async Task<IEnumerable<T>> FindAsync(Expression<Func<T, bool>> predicate)
        {
            return await _dbSet.Where(predicate).ToListAsync();
        }

        public async Task AddAsync(T entity)
        {
            await _dbSet.AddAsync(entity);
            await _context.SaveChangesAsync();
        }

        public async Task UpdateAsync(T entity)
        {
            var entry = _context.Entry(entity);

            if (entry.State == EntityState.Detached)
            {
                var entityType = _context.Model.FindEntityType(typeof(T));
                var primaryKey = entityType?.FindPrimaryKey();

                if (primaryKey is not null)
                {
                    var localEntity = _dbSet.Local
                        .FirstOrDefault(localItem =>
                            primaryKey.Properties.All(property =>
                                Equals(
                                    property.PropertyInfo?.GetValue(localItem),
                                    property.PropertyInfo?.GetValue(entity)
                                )));

                    if (localEntity is not null)
                    {
                        _context.Entry(localEntity).State = EntityState.Detached;
                    }
                }

                _dbSet.Attach(entity);
            }

            _context.Entry(entity).State = EntityState.Modified;
            await _context.SaveChangesAsync();
        }

        public async Task RemoveAsync(T entity)
        {
            if (_context.Entry(entity).State == EntityState.Detached)
            {
                _dbSet.Attach(entity);
            }
            _dbSet.Remove(entity);
            await _context.SaveChangesAsync();
        }

        public async Task DeleteAsync(int id)
        {
            T? entity = await _dbSet.FindAsync(id);
            if (entity != null)
            {
                await RemoveAsync(entity);
            }
        }

        public async Task<bool> ExistsAsync(int id)
        {
            var entity = await _dbSet.FindAsync(id);
            return entity != null;
        }
    }
}
