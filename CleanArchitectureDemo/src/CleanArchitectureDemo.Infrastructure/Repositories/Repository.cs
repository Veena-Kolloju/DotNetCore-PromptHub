using System.Linq.Expressions;
using CleanArchitectureDemo.Domain.Common;
using CleanArchitectureDemo.Domain.Entities;
using CleanArchitectureDemo.Domain.Interfaces;
using CleanArchitectureDemo.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace CleanArchitectureDemo.Infrastructure.Repositories;

public class Repository<T> : IRepository<T> where T : BaseEntity
{
    protected readonly ApplicationDbContext _context;
    protected readonly DbSet<T> _dbSet;

    public Repository(ApplicationDbContext context)
    {
        _context = context;
        _dbSet = context.Set<T>();
    }

    public virtual async Task<T?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return await _dbSet.FindAsync(new object[] { id }, cancellationToken);
    }

    public virtual async Task<IEnumerable<T>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        return await _dbSet.Where(x => !x.IsDeleted).ToListAsync(cancellationToken);
    }

    public virtual async Task<PagedResult<T>> GetPagedAsync(int pageNumber, int pageSize, CancellationToken cancellationToken = default)
    {
        var totalCount = await _dbSet.Where(x => !x.IsDeleted).CountAsync(cancellationToken);
        var items = await _dbSet
            .Where(x => !x.IsDeleted)
            .Skip((pageNumber - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);

        return new PagedResult<T>
        {
            Items = items,
            TotalCount = totalCount,
            PageNumber = pageNumber,
            PageSize = pageSize
        };
    }

    public virtual async Task<IEnumerable<T>> FindAsync(Expression<Func<T, bool>> predicate, CancellationToken cancellationToken = default)
    {
        return await _dbSet.Where(predicate).Where(x => !x.IsDeleted).ToListAsync(cancellationToken);
    }

    public virtual async Task<T?> SingleOrDefaultAsync(Expression<Func<T, bool>> predicate, CancellationToken cancellationToken = default)
    {
        return await _dbSet.Where(predicate).Where(x => !x.IsDeleted).SingleOrDefaultAsync(cancellationToken);
    }

    public virtual async Task AddAsync(T entity, CancellationToken cancellationToken = default)
    {
        await _dbSet.AddAsync(entity, cancellationToken);
    }

    public virtual async Task AddRangeAsync(IEnumerable<T> entities, CancellationToken cancellationToken = default)
    {
        await _dbSet.AddRangeAsync(entities, cancellationToken);
    }

    public virtual void Update(T entity)
    {
        _dbSet.Update(entity);
    }

    public virtual void Remove(T entity)
    {
        entity.MarkAsDeleted();
        Update(entity);
    }

    public virtual async Task<bool> ExistsAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return await _dbSet.AnyAsync(x => x.Id == id && !x.IsDeleted, cancellationToken);
    }

    public virtual async Task<int> CountAsync(Expression<Func<T, bool>>? predicate = null, CancellationToken cancellationToken = default)
    {
        var query = _dbSet.Where(x => !x.IsDeleted);
        if (predicate != null)
            query = query.Where(predicate);
        return await query.CountAsync(cancellationToken);
    }
}