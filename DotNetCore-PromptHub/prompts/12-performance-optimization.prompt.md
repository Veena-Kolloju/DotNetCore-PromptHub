---
context: ../design-system/architecture-patterns.md
context: ../instructions/general-coding.instructions.md
---

# Performance Optimization Framework

Implement comprehensive performance optimization strategies for .NET Core applications including caching, async patterns, and database optimization.

## Requirements

### 1. Caching Strategies
- In-memory caching with IMemoryCache
- Distributed caching with Redis
- Response caching for API endpoints
- Cache invalidation patterns

### 2. Database Optimization
- Query optimization and indexing
- Connection pooling configuration
- Bulk operations for large datasets
- Read/write splitting strategies

## Example Implementation

### Caching Service
```csharp
public interface ICacheService
{
    Task<T> GetAsync<T>(string key, CancellationToken cancellationToken = default);
    Task SetAsync<T>(string key, T value, TimeSpan? expiration = null, CancellationToken cancellationToken = default);
    Task RemoveAsync(string key, CancellationToken cancellationToken = default);
    Task RemoveByPatternAsync(string pattern, CancellationToken cancellationToken = default);
}

public class CacheService : ICacheService
{
    private readonly IMemoryCache _memoryCache;
    private readonly IDistributedCache _distributedCache;
    private readonly ILogger<CacheService> _logger;

    public CacheService(IMemoryCache memoryCache, IDistributedCache distributedCache, ILogger<CacheService> logger)
    {
        _memoryCache = memoryCache;
        _distributedCache = distributedCache;
        _logger = logger;
    }

    public async Task<T> GetAsync<T>(string key, CancellationToken cancellationToken = default)
    {
        // Try memory cache first
        if (_memoryCache.TryGetValue(key, out T cachedValue))
        {
            _logger.LogDebug("Cache hit for key {Key} in memory cache", key);
            return cachedValue;
        }

        // Try distributed cache
        var distributedValue = await _distributedCache.GetStringAsync(key, cancellationToken);
        if (!string.IsNullOrEmpty(distributedValue))
        {
            var deserializedValue = JsonSerializer.Deserialize<T>(distributedValue);
            
            // Store in memory cache for faster access
            _memoryCache.Set(key, deserializedValue, TimeSpan.FromMinutes(5));
            
            _logger.LogDebug("Cache hit for key {Key} in distributed cache", key);
            return deserializedValue;
        }

        _logger.LogDebug("Cache miss for key {Key}", key);
        return default(T);
    }

    public async Task SetAsync<T>(string key, T value, TimeSpan? expiration = null, CancellationToken cancellationToken = default)
    {
        var options = new MemoryCacheEntryOptions
        {
            AbsoluteExpirationRelativeToNow = expiration ?? TimeSpan.FromMinutes(30)
        };
        _memoryCache.Set(key, value, options);

        var serializedValue = JsonSerializer.Serialize(value);
        var distributedOptions = new DistributedCacheEntryOptions
        {
            AbsoluteExpirationRelativeToNow = expiration ?? TimeSpan.FromHours(1)
        };
        
        await _distributedCache.SetStringAsync(key, serializedValue, distributedOptions, cancellationToken);
        _logger.LogDebug("Cached value for key {Key}", key);
    }
}
```

### Optimized Repository
```csharp
public class OptimizedCustomerRepository : ICustomerRepository
{
    private readonly ApplicationDbContext _context;
    private readonly ICacheService _cache;

    public OptimizedCustomerRepository(ApplicationDbContext context, ICacheService cache)
    {
        _context = context;
        _cache = cache;
    }

    public async Task<Customer> GetByIdAsync(int id, CancellationToken cancellationToken = default)
    {
        var cacheKey = $"customer_{id}";
        var cachedCustomer = await _cache.GetAsync<Customer>(cacheKey, cancellationToken);
        
        if (cachedCustomer != null)
            return cachedCustomer;

        var customer = await _context.Customers
            .AsNoTracking()
            .Include(c => c.Orders.Take(10)) // Limit related data
            .FirstOrDefaultAsync(c => c.Id == id, cancellationToken);

        if (customer != null)
        {
            await _cache.SetAsync(cacheKey, customer, TimeSpan.FromMinutes(15), cancellationToken);
        }

        return customer;
    }

    public async Task<PagedResult<Customer>> GetPagedOptimizedAsync(int pageNumber, int pageSize, CancellationToken cancellationToken = default)
    {
        var query = _context.Customers.AsNoTracking();
        
        var totalCount = await query.CountAsync(cancellationToken);
        
        var customers = await query
            .OrderBy(c => c.Name)
            .Skip((pageNumber - 1) * pageSize)
            .Take(pageSize)
            .Select(c => new Customer // Projection to avoid loading unnecessary data
            {
                Id = c.Id,
                Name = c.Name,
                Email = c.Email,
                Type = c.Type,
                CreatedAt = c.CreatedAt
            })
            .ToListAsync(cancellationToken);

        return new PagedResult<Customer>
        {
            Items = customers,
            TotalCount = totalCount,
            PageNumber = pageNumber,
            PageSize = pageSize
        };
    }

    public async Task BulkInsertAsync(IEnumerable<Customer> customers, CancellationToken cancellationToken = default)
    {
        const int batchSize = 1000;
        var customerList = customers.ToList();
        
        for (int i = 0; i < customerList.Count; i += batchSize)
        {
            var batch = customerList.Skip(i).Take(batchSize);
            _context.Customers.AddRange(batch);
            await _context.SaveChangesAsync(cancellationToken);
            _context.ChangeTracker.Clear(); // Clear to avoid memory issues
        }
    }
}
```

### Performance Monitoring
```csharp
public class PerformanceMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<PerformanceMiddleware> _logger;
    private readonly IMetricsCollector _metrics;

    public PerformanceMiddleware(RequestDelegate next, ILogger<PerformanceMiddleware> logger, IMetricsCollector metrics)
    {
        _next = next;
        _logger = logger;
        _metrics = metrics;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        var stopwatch = Stopwatch.StartNew();
        var path = context.Request.Path.Value;
        var method = context.Request.Method;

        try
        {
            await _next(context);
        }
        finally
        {
            stopwatch.Stop();
            var elapsed = stopwatch.ElapsedMilliseconds;

            _metrics.RecordTime("http_request_duration", TimeSpan.FromMilliseconds(elapsed), new Dictionary<string, string>
            {
                ["method"] = method,
                ["path"] = path,
                ["status_code"] = context.Response.StatusCode.ToString()
            });

            if (elapsed > 1000) // Log slow requests
            {
                _logger.LogWarning("Slow request: {Method} {Path} took {ElapsedMs}ms", method, path, elapsed);
            }
        }
    }
}
```

### Async Best Practices
```csharp
public class OptimizedCustomerService
{
    private readonly ICustomerRepository _repository;
    private readonly IEmailService _emailService;
    private readonly IPaymentService _paymentService;

    public async Task<Result<CustomerDto>> ProcessCustomerAsync(CreateCustomerRequest request, CancellationToken cancellationToken = default)
    {
        // Parallel execution for independent operations
        var validationTask = ValidateCustomerDataAsync(request, cancellationToken);
        var duplicateCheckTask = CheckForDuplicateAsync(request.Email, cancellationToken);

        await Task.WhenAll(validationTask, duplicateCheckTask);

        var validationResult = await validationTask;
        var isDuplicate = await duplicateCheckTask;

        if (!validationResult.IsValid || isDuplicate)
        {
            return Result<CustomerDto>.Failure("Validation failed");
        }

        var customer = await CreateCustomerAsync(request, cancellationToken);

        // Fire and forget for non-critical operations
        _ = Task.Run(async () =>
        {
            try
            {
                await _emailService.SendWelcomeEmailAsync(customer.Email.Value, customer.Name, CancellationToken.None);
            }
            catch (Exception ex)
            {
                // Log but don't fail the main operation
                _logger.LogError(ex, "Failed to send welcome email for customer {CustomerId}", customer.Id);
            }
        }, cancellationToken);

        return Result<CustomerDto>.Success(_mapper.Map<CustomerDto>(customer));
    }

    private async Task<ValidationResult> ValidateCustomerDataAsync(CreateCustomerRequest request, CancellationToken cancellationToken)
    {
        // Simulate async validation (e.g., external service call)
        await Task.Delay(100, cancellationToken);
        return new ValidationResult { IsValid = !string.IsNullOrEmpty(request.Name) };
    }
}
```

## Deliverables

1. **Caching Framework**: Multi-level caching implementation
2. **Database Optimization**: Query and connection optimization
3. **Async Patterns**: Proper async/await usage
4. **Performance Monitoring**: Metrics and logging
5. **Memory Management**: Efficient resource usage
6. **Bulk Operations**: Large dataset handling
7. **Connection Pooling**: Database connection optimization
8. **Response Compression**: HTTP response optimization
9. **Load Testing**: Performance validation
10. **Profiling Tools**: Performance analysis setup

## Validation Checklist

- [ ] Caching strategy implemented effectively
- [ ] Database queries optimized with proper indexing
- [ ] Async patterns used correctly throughout
- [ ] Memory usage monitored and optimized
- [ ] Performance metrics collected and analyzed
- [ ] Bulk operations handle large datasets efficiently
- [ ] Connection pooling configured properly
- [ ] Response times meet requirements
- [ ] Load testing validates performance under stress
- [ ] Profiling identifies and resolves bottlenecks