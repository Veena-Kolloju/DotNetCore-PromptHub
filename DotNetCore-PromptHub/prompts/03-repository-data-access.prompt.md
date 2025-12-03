---
context: ../design-system/architecture-patterns.md
context: ../instructions/general-coding.instructions.md
---

# Repository & Data Access Layer

Create comprehensive data access layer with repository pattern, Entity Framework Core, and transaction management.

## Requirements

### 1. Repository Pattern
- Generic repository interface and implementation
- Specific repositories for complex queries
- Unit of Work pattern for transaction management
- Specification pattern for query composition

### 2. Entity Framework Configuration
- DbContext with proper configuration
- Entity configurations using Fluent API
- Migration management
- Connection string management

### 3. Query Optimization
- Pagination support
- Filtering and sorting
- Soft delete implementation
- Performance optimization techniques

## Example Implementation

### Generic Repository Interface
```csharp
public interface IRepository<T> where T : BaseEntity
{
    Task<T> GetByIdAsync(int id, CancellationToken cancellationToken = default);
    Task<IEnumerable<T>> GetAllAsync(CancellationToken cancellationToken = default);
    Task<PagedResult<T>> GetPagedAsync(int pageNumber, int pageSize, CancellationToken cancellationToken = default);
    Task<IEnumerable<T>> FindAsync(Expression<Func<T, bool>> predicate, CancellationToken cancellationToken = default);
    Task<T> SingleOrDefaultAsync(Expression<Func<T, bool>> predicate, CancellationToken cancellationToken = default);
    Task AddAsync(T entity, CancellationToken cancellationToken = default);
    Task AddRangeAsync(IEnumerable<T> entities, CancellationToken cancellationToken = default);
    void Update(T entity);
    void Remove(T entity);
    Task<bool> ExistsAsync(int id, CancellationToken cancellationToken = default);
    Task<int> CountAsync(Expression<Func<T, bool>> predicate = null, CancellationToken cancellationToken = default);
}
```

### Generic Repository Implementation
```csharp
public class Repository<T> : IRepository<T> where T : BaseEntity
{
    protected readonly ApplicationDbContext _context;
    protected readonly DbSet<T> _dbSet;

    public Repository(ApplicationDbContext context)
    {
        _context = context;
        _dbSet = context.Set<T>();
    }

    public virtual async Task<T> GetByIdAsync(int id, CancellationToken cancellationToken = default)
    {
        return await _dbSet.FindAsync(new object[] { id }, cancellationToken);
    }

    public virtual async Task<IEnumerable<T>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        return await _dbSet.ToListAsync(cancellationToken);
    }

    public virtual async Task<PagedResult<T>> GetPagedAsync(int pageNumber, int pageSize, CancellationToken cancellationToken = default)
    {
        var totalCount = await _dbSet.CountAsync(cancellationToken);
        var items = await _dbSet
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

    public virtual async Task AddAsync(T entity, CancellationToken cancellationToken = default)
    {
        await _dbSet.AddAsync(entity, cancellationToken);
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
}
```

### Specific Repository Example
```csharp
public interface ICustomerRepository : IRepository<Customer>
{
    Task<Customer> GetByEmailAsync(string email, CancellationToken cancellationToken = default);
    Task<bool> ExistsByEmailAsync(string email, CancellationToken cancellationToken = default);
    Task<IEnumerable<Customer>> GetVipCustomersAsync(CancellationToken cancellationToken = default);
    Task<PagedResult<Customer>> SearchAsync(CustomerSearchCriteria criteria, CancellationToken cancellationToken = default);
}

public class CustomerRepository : Repository<Customer>, ICustomerRepository
{
    public CustomerRepository(ApplicationDbContext context) : base(context) { }

    public async Task<Customer> GetByEmailAsync(string email, CancellationToken cancellationToken = default)
    {
        return await _dbSet
            .Include(c => c.Orders)
            .FirstOrDefaultAsync(c => c.Email.Value == email, cancellationToken);
    }

    public async Task<bool> ExistsByEmailAsync(string email, CancellationToken cancellationToken = default)
    {
        return await _dbSet.AnyAsync(c => c.Email.Value == email, cancellationToken);
    }

    public async Task<IEnumerable<Customer>> GetVipCustomersAsync(CancellationToken cancellationToken = default)
    {
        return await _dbSet
            .Where(c => c.Type == CustomerType.Vip)
            .OrderBy(c => c.Name)
            .ToListAsync(cancellationToken);
    }

    public async Task<PagedResult<Customer>> SearchAsync(CustomerSearchCriteria criteria, CancellationToken cancellationToken = default)
    {
        var query = _dbSet.AsQueryable();

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
```

### Unit of Work Pattern
```csharp
public interface IUnitOfWork : IDisposable
{
    ICustomerRepository Customers { get; }
    IOrderRepository Orders { get; }
    IProductRepository Products { get; }
    
    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
    Task BeginTransactionAsync(CancellationToken cancellationToken = default);
    Task CommitTransactionAsync(CancellationToken cancellationToken = default);
    Task RollbackTransactionAsync(CancellationToken cancellationToken = default);
}

public class UnitOfWork : IUnitOfWork
{
    private readonly ApplicationDbContext _context;
    private IDbContextTransaction _transaction;

    public UnitOfWork(ApplicationDbContext context)
    {
        _context = context;
        Customers = new CustomerRepository(_context);
        Orders = new OrderRepository(_context);
        Products = new ProductRepository(_context);
    }

    public ICustomerRepository Customers { get; }
    public IOrderRepository Orders { get; }
    public IProductRepository Products { get; }

    public async Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        return await _context.SaveChangesAsync(cancellationToken);
    }

    public async Task BeginTransactionAsync(CancellationToken cancellationToken = default)
    {
        _transaction = await _context.Database.BeginTransactionAsync(cancellationToken);
    }

    public async Task CommitTransactionAsync(CancellationToken cancellationToken = default)
    {
        if (_transaction != null)
        {
            await _transaction.CommitAsync(cancellationToken);
            await _transaction.DisposeAsync();
            _transaction = null;
        }
    }

    public async Task RollbackTransactionAsync(CancellationToken cancellationToken = default)
    {
        if (_transaction != null)
        {
            await _transaction.RollbackAsync(cancellationToken);
            await _transaction.DisposeAsync();
            _transaction = null;
        }
    }

    public void Dispose()
    {
        _transaction?.Dispose();
        _context?.Dispose();
    }
}
```

## Deliverables

1. **Generic Repository**: Interface and implementation
2. **Specific Repositories**: Domain-specific query methods
3. **Unit of Work**: Transaction management
4. **DbContext Configuration**: Entity Framework setup
5. **Entity Configurations**: Fluent API configurations
6. **Specification Pattern**: Query composition
7. **Pagination Support**: Paged result implementation
8. **Search Criteria**: Filtering and sorting models
9. **Soft Delete**: Logical deletion implementation
10. **Performance Optimization**: Query optimization techniques

## Validation Checklist

- [ ] Repository pattern properly implemented
- [ ] Unit of Work manages transactions
- [ ] Entity Framework configured correctly
- [ ] Pagination implemented efficiently
- [ ] Soft delete functionality working
- [ ] Query optimization applied
- [ ] Async operations used throughout
- [ ] Cancellation tokens supported
- [ ] Connection strings secured
- [ ] Migration strategy defined