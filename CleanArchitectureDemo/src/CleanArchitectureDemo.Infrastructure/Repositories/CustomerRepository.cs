using CleanArchitectureDemo.Domain.Common;
using CleanArchitectureDemo.Domain.Entities;
using CleanArchitectureDemo.Domain.Enums;
using CleanArchitectureDemo.Domain.Interfaces;
using CleanArchitectureDemo.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace CleanArchitectureDemo.Infrastructure.Repositories;

public class CustomerRepository : Repository<Customer>, ICustomerRepository
{
    public CustomerRepository(ApplicationDbContext context) : base(context) { }

    public async Task<Customer?> GetByEmailAsync(string email, CancellationToken cancellationToken = default)
    {
        return await _dbSet
            .Where(c => !c.IsDeleted)
            .FirstOrDefaultAsync(c => c.Email.Value == email, cancellationToken);
    }

    public async Task<bool> ExistsByEmailAsync(string email, CancellationToken cancellationToken = default)
    {
        return await _dbSet.AnyAsync(c => c.Email.Value == email && !c.IsDeleted, cancellationToken);
    }

    public async Task<IEnumerable<Customer>> GetVipCustomersAsync(CancellationToken cancellationToken = default)
    {
        return await _dbSet
            .Where(c => c.Type == CustomerType.Vip && !c.IsDeleted)
            .OrderBy(c => c.Name)
            .ToListAsync(cancellationToken);
    }

    public async Task<PagedResult<Customer>> SearchAsync(CustomerSearchCriteria criteria, CancellationToken cancellationToken = default)
    {
        var query = _dbSet.Where(c => !c.IsDeleted).AsQueryable();

        if (!string.IsNullOrWhiteSpace(criteria.Name))
            query = query.Where(c => c.Name.Contains(criteria.Name));

        if (!string.IsNullOrWhiteSpace(criteria.Email))
            query = query.Where(c => c.Email.Value.Contains(criteria.Email));

        if (criteria.Type.HasValue)
            query = query.Where(c => c.Type == criteria.Type.Value);

        if (criteria.Status.HasValue)
            query = query.Where(c => c.Status == criteria.Status.Value);

        var totalCount = await query.CountAsync(cancellationToken);
        var items = await query
            .OrderBy(c => c.Name)
            .Skip((criteria.PageNumber - 1) * criteria.PageSize)
            .Take(criteria.PageSize)
            .ToListAsync(cancellationToken);

        return new PagedResult<Customer>
        {
            Items = items,
            TotalCount = totalCount,
            PageNumber = criteria.PageNumber,
            PageSize = criteria.PageSize
        };
    }
}