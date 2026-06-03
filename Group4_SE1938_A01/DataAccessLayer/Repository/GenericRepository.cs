using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Domain.Models;

namespace DataAccessLayer.Repository
{
    public class GenericRepository<T> : IGenericRepository<T> where T : class
    {
        protected readonly Prn222AssigmentContext _context;
        protected readonly DbSet<T> _dbSet;
        private readonly ILogger<GenericRepository<T>> _logger;

        public GenericRepository(Prn222AssigmentContext context, ILogger<GenericRepository<T>> logger)
        {
            _context = context;
            _dbSet = context.Set<T>();
            _logger = logger;
        }

        public async Task<IEnumerable<T>> GetAllAsync(
            Expression<Func<T, bool>>? filter = null,
            Func<IQueryable<T>, IOrderedQueryable<T>>? orderBy = null,
            string includeProperties = "")
        {
            IQueryable<T> query = _dbSet;

            if (filter != null)
            {
                query = query.Where(filter);
            }

            foreach (var includeProperty in includeProperties.Split(new char[] { ',' }, StringSplitOptions.RemoveEmptyEntries))
            {
                query = query.Include(includeProperty.Trim());
            }

            if (orderBy != null)
            {
                return await orderBy(query).ToListAsync();
            }
            else
            {
                return await query.ToListAsync();
            }
        }

        public async Task<T?> GetByIdAsync(object id)
        {
            return await _dbSet.FindAsync(id);
        }

        public async Task<T?> GetFirstOrDefaultAsync(Expression<Func<T, bool>> filter, string includeProperties = "")
        {
            IQueryable<T> query = _dbSet;

            foreach (var includeProperty in includeProperties.Split(new char[] { ',' }, StringSplitOptions.RemoveEmptyEntries))
            {
                query = query.Include(includeProperty.Trim());
            }

            return await query.FirstOrDefaultAsync(filter);
        }

        public async Task AddAsync(T entity)
        {
            await _dbSet.AddAsync(entity);
        }

        public void Update(T entity)
        {
            _dbSet.Attach(entity);
            _context.Entry(entity).State = EntityState.Modified;
        }

        public void Delete(T entity)
        {
            if (_context.Entry(entity).State == EntityState.Detached)
            {
                _dbSet.Attach(entity);
            }
            _dbSet.Remove(entity);
        }

        public async Task DeleteByIdAsync(object id)
        {
            T? entityToDelete = await _dbSet.FindAsync(id);
            if (entityToDelete != null)
            {
                Delete(entityToDelete);
            }
        }

        public async Task SaveAsync()
        {
            try
            {
                await _context.SaveChangesAsync();
            }
            catch (DbUpdateException dbEx)
            {
                var entityNames = dbEx.Entries
                    .Select(e => $"{e.Metadata.ClrType.Name}({e.State})")
                    .Distinct()
                    .ToList();

                string entityInfo = entityNames.Count > 0
                    ? string.Join(", ", entityNames)
                    : typeof(T).Name;

                string fullMessage = BuildExceptionChain(dbEx);
                _logger.LogError(dbEx, "DbUpdateException while saving {EntityInfo}. Details: {Details}", entityInfo, fullMessage);

                throw new InvalidOperationException(
                    $"SaveChanges failed for {entityInfo}. Details: {fullMessage}",
                    dbEx);
            }
        }

        private static string BuildExceptionChain(Exception ex)
        {
            var messages = new List<string>();
            Exception? current = ex;
            while (current != null)
            {
                messages.Add(current.Message);
                current = current.InnerException;
            }

            return string.Join(" | INNER => ", messages);
        }
    }
}
