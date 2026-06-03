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

        Task<T?> GetByIdAsync(object id);
        
        Task<T?> GetFirstOrDefaultAsync(
            Expression<Func<T, bool>> filter, 
            string includeProperties = "");

        Task AddAsync(T entity);

        void Update(T entity);

        void Delete(T entity);

        Task DeleteByIdAsync(object id);

        Task SaveAsync();
    }
}
