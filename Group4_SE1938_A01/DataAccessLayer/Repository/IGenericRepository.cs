using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Threading.Tasks;

namespace DataAccessLayer.Repository
{
    public interface IGenericRepository<T> where T : class
    {
        Task<IEnumerable<T>> GetAllAsync(
            Expression<Func<T, bool>>? filter = null,
            Func<IQueryable<T>, IOrderedQueryable<T>>? orderBy = null,
            string includeProperties = "");

        /// <summary>
        /// Same as GetAllAsync but uses AsNoTracking for read-only queries (better performance).
        /// </summary>
        Task<IEnumerable<T>> GetAllNoTrackingAsync(
            Expression<Func<T, bool>>? filter = null,
            Func<IQueryable<T>, IOrderedQueryable<T>>? orderBy = null,
            string includeProperties = "");

        Task<T?> GetByIdAsync(object id);
        
        Task<T?> GetFirstOrDefaultAsync(
            Expression<Func<T, bool>> filter, 
            string includeProperties = "");

        Task AddAsync(T entity);

        Task AddRangeAsync(IEnumerable<T> entities);

        void Update(T entity);

        void Delete(T entity);

        Task DeleteByIdAsync(object id);

        Task SaveAsync();
    }
}
