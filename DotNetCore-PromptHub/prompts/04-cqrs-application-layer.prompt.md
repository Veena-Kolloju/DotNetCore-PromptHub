---
context: ../design-system/architecture-patterns.md
context: ../instructions/general-coding.instructions.md
---

# CQRS & Application Layer

Create comprehensive CQRS implementation with commands, queries, handlers, and cross-cutting concerns using MediatR.

## Requirements

### 1. Command/Query Separation
- Commands for write operations
- Queries for read operations
- Separate handlers for each operation
- Request/Response pattern implementation

### 2. Pipeline Behaviors
- Validation behavior using FluentValidation
- Logging behavior with structured logging
- Performance monitoring behavior
- Exception handling behavior

### 3. Feature Organization
- Organize by business features
- Vertical slice architecture
- Clear separation of concerns
- Testable handler implementations

## Example Implementation

### Command Example
```csharp
// Command
public record CreateCustomerCommand : IRequest<Result<CustomerDto>>
{
    public string Name { get; init; }
    public string Email { get; init; }
    public string Phone { get; init; }
    public AddressDto Address { get; init; }
}

// Handler
public class CreateCustomerHandler : IRequestHandler<CreateCustomerCommand, Result<CustomerDto>>
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly IMapper _mapper;
    private readonly ILogger<CreateCustomerHandler> _logger;

    public CreateCustomerHandler(
        IUnitOfWork unitOfWork,
        IMapper mapper,
        ILogger<CreateCustomerHandler> logger)
    {
        _unitOfWork = unitOfWork;
        _mapper = mapper;
        _logger = logger;
    }

    public async Task<Result<CustomerDto>> Handle(CreateCustomerCommand request, CancellationToken cancellationToken)
    {
        try
        {
            // Check if email already exists
            var existingCustomer = await _unitOfWork.Customers.GetByEmailAsync(request.Email, cancellationToken);
            if (existingCustomer != null)
            {
                return Result<CustomerDto>.Failure("Customer with this email already exists");
            }

            // Create domain entity
            var customer = new Customer(
                request.Name,
                new Email(request.Email),
                new PhoneNumber(request.Phone));

            if (request.Address != null)
            {
                customer.UpdateAddress(new Address(
                    request.Address.Street,
                    request.Address.City,
                    request.Address.State,
                    request.Address.ZipCode,
                    request.Address.Country));
            }

            // Save to repository
            await _unitOfWork.Customers.AddAsync(customer, cancellationToken);
            await _unitOfWork.SaveChangesAsync(cancellationToken);

            // Map to DTO and return
            var customerDto = _mapper.Map<CustomerDto>(customer);
            
            _logger.LogInformation("Customer created successfully with ID {CustomerId}", customer.Id);
            
            return Result<CustomerDto>.Success(customerDto);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating customer with email {Email}", request.Email);
            return Result<CustomerDto>.Failure("An error occurred while creating the customer");
        }
    }
}

// Validator
public class CreateCustomerValidator : AbstractValidator<CreateCustomerCommand>
{
    public CreateCustomerValidator()
    {
        RuleFor(x => x.Name)
            .NotEmpty().WithMessage("Name is required")
            .MaximumLength(100).WithMessage("Name cannot exceed 100 characters");

        RuleFor(x => x.Email)
            .NotEmpty().WithMessage("Email is required")
            .EmailAddress().WithMessage("Invalid email format");

        RuleFor(x => x.Phone)
            .NotEmpty().WithMessage("Phone is required")
            .Matches(@"^\+?[1-9]\d{1,14}$").WithMessage("Invalid phone format");

        RuleFor(x => x.Address)
            .SetValidator(new AddressDtoValidator())
            .When(x => x.Address != null);
    }
}
```

### Query Example
```csharp
// Query
public record GetCustomerByIdQuery(int Id) : IRequest<Result<CustomerDto>>;

// Handler
public class GetCustomerByIdHandler : IRequestHandler<GetCustomerByIdQuery, Result<CustomerDto>>
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly IMapper _mapper;
    private readonly IMemoryCache _cache;
    private readonly ILogger<GetCustomerByIdHandler> _logger;

    public GetCustomerByIdHandler(
        IUnitOfWork unitOfWork,
        IMapper mapper,
        IMemoryCache cache,
        ILogger<GetCustomerByIdHandler> logger)
    {
        _unitOfWork = unitOfWork;
        _mapper = mapper;
        _cache = cache;
        _logger = logger;
    }

    public async Task<Result<CustomerDto>> Handle(GetCustomerByIdQuery request, CancellationToken cancellationToken)
    {
        var cacheKey = $"customer_{request.Id}";
        
        if (_cache.TryGetValue(cacheKey, out CustomerDto cachedCustomer))
        {
            _logger.LogDebug("Customer {CustomerId} retrieved from cache", request.Id);
            return Result<CustomerDto>.Success(cachedCustomer);
        }

        var customer = await _unitOfWork.Customers.GetByIdAsync(request.Id, cancellationToken);
        
        if (customer == null)
        {
            _logger.LogWarning("Customer with ID {CustomerId} not found", request.Id);
            return Result<CustomerDto>.Failure("Customer not found");
        }

        var customerDto = _mapper.Map<CustomerDto>(customer);
        
        // Cache for 5 minutes
        _cache.Set(cacheKey, customerDto, TimeSpan.FromMinutes(5));
        
        _logger.LogDebug("Customer {CustomerId} retrieved from database", request.Id);
        
        return Result<CustomerDto>.Success(customerDto);
    }
}

// Search Query
public record SearchCustomersQuery : IRequest<Result<PagedResult<CustomerSummaryDto>>>
{
    public string Name { get; init; }
    public string Email { get; init; }
    public CustomerType? Type { get; init; }
    public CustomerStatus? Status { get; init; }
    public int PageNumber { get; init; } = 1;
    public int PageSize { get; init; } = 10;
    public string SortBy { get; init; } = "Name";
    public string SortDirection { get; init; } = "asc";
}

public class SearchCustomersHandler : IRequestHandler<SearchCustomersQuery, Result<PagedResult<CustomerSummaryDto>>>
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly IMapper _mapper;

    public SearchCustomersHandler(IUnitOfWork unitOfWork, IMapper mapper)
    {
        _unitOfWork = unitOfWork;
        _mapper = mapper;
    }

    public async Task<Result<PagedResult<CustomerSummaryDto>>> Handle(SearchCustomersQuery request, CancellationToken cancellationToken)
    {
        var criteria = new CustomerSearchCriteria
        {
            Name = request.Name,
            Email = request.Email,
            Type = request.Type,
            Status = request.Status,
            PageNumber = request.PageNumber,
            PageSize = request.PageSize,
            SortBy = request.SortBy,
            SortDirection = request.SortDirection
        };

        var result = await _unitOfWork.Customers.SearchAsync(criteria, cancellationToken);
        var dtoResult = new PagedResult<CustomerSummaryDto>
        {
            Items = _mapper.Map<List<CustomerSummaryDto>>(result.Items),
            TotalCount = result.TotalCount,
            PageNumber = result.PageNumber,
            PageSize = result.PageSize
        };

        return Result<PagedResult<CustomerSummaryDto>>.Success(dtoResult);
    }
}
```

### Pipeline Behaviors
```csharp
// Validation Behavior
public class ValidationBehavior<TRequest, TResponse> : IPipelineBehavior<TRequest, TResponse>
    where TRequest : IRequest<TResponse>
{
    private readonly IEnumerable<IValidator<TRequest>> _validators;

    public ValidationBehavior(IEnumerable<IValidator<TRequest>> validators)
    {
        _validators = validators;
    }

    public async Task<TResponse> Handle(TRequest request, RequestHandlerDelegate<TResponse> next, CancellationToken cancellationToken)
    {
        if (_validators.Any())
        {
            var context = new ValidationContext<TRequest>(request);
            var validationResults = await Task.WhenAll(_validators.Select(v => v.ValidateAsync(context, cancellationToken)));
            var failures = validationResults.SelectMany(r => r.Errors).Where(f => f != null).ToList();

            if (failures.Any())
            {
                var errorMessage = string.Join("; ", failures.Select(f => f.ErrorMessage));
                
                if (typeof(TResponse).IsGenericType && typeof(TResponse).GetGenericTypeDefinition() == typeof(Result<>))
                {
                    var resultType = typeof(TResponse).GetGenericArguments()[0];
                    var failureMethod = typeof(Result<>).MakeGenericType(resultType).GetMethod("Failure", new[] { typeof(string) });
                    return (TResponse)failureMethod.Invoke(null, new object[] { errorMessage });
                }
                
                throw new ValidationException(failures);
            }
        }

        return await next();
    }
}

// Logging Behavior
public class LoggingBehavior<TRequest, TResponse> : IPipelineBehavior<TRequest, TResponse>
    where TRequest : IRequest<TResponse>
{
    private readonly ILogger<LoggingBehavior<TRequest, TResponse>> _logger;

    public LoggingBehavior(ILogger<LoggingBehavior<TRequest, TResponse>> logger)
    {
        _logger = logger;
    }

    public async Task<TResponse> Handle(TRequest request, RequestHandlerDelegate<TResponse> next, CancellationToken cancellationToken)
    {
        var requestName = typeof(TRequest).Name;
        var requestId = Guid.NewGuid();

        _logger.LogInformation("Handling {RequestName} with ID {RequestId}", requestName, requestId);

        var stopwatch = Stopwatch.StartNew();
        
        try
        {
            var response = await next();
            
            stopwatch.Stop();
            _logger.LogInformation("Completed {RequestName} with ID {RequestId} in {ElapsedMs}ms", 
                requestName, requestId, stopwatch.ElapsedMilliseconds);
            
            return response;
        }
        catch (Exception ex)
        {
            stopwatch.Stop();
            _logger.LogError(ex, "Error handling {RequestName} with ID {RequestId} after {ElapsedMs}ms", 
                requestName, requestId, stopwatch.ElapsedMilliseconds);
            throw;
        }
    }
}

// Performance Behavior
public class PerformanceBehavior<TRequest, TResponse> : IPipelineBehavior<TRequest, TResponse>
    where TRequest : IRequest<TResponse>
{
    private readonly ILogger<PerformanceBehavior<TRequest, TResponse>> _logger;
    private readonly Stopwatch _timer;

    public PerformanceBehavior(ILogger<PerformanceBehavior<TRequest, TResponse>> logger)
    {
        _logger = logger;
        _timer = new Stopwatch();
    }

    public async Task<TResponse> Handle(TRequest request, RequestHandlerDelegate<TResponse> next, CancellationToken cancellationToken)
    {
        _timer.Start();

        var response = await next();

        _timer.Stop();

        var elapsedMilliseconds = _timer.ElapsedMilliseconds;

        if (elapsedMilliseconds > 500) // Log if request takes longer than 500ms
        {
            var requestName = typeof(TRequest).Name;
            _logger.LogWarning("Long Running Request: {Name} ({ElapsedMilliseconds} milliseconds) {@Request}",
                requestName, elapsedMilliseconds, request);
        }

        return response;
    }
}
```

### Result Pattern
```csharp
public class Result<T>
{
    public bool IsSuccess { get; private set; }
    public T Data { get; private set; }
    public string ErrorMessage { get; private set; }
    public List<string> Errors { get; private set; } = new();

    private Result(bool isSuccess, T data, string errorMessage, List<string> errors = null)
    {
        IsSuccess = isSuccess;
        Data = data;
        ErrorMessage = errorMessage;
        Errors = errors ?? new List<string>();
    }

    public static Result<T> Success(T data) => new(true, data, null);
    public static Result<T> Failure(string errorMessage) => new(false, default, errorMessage);
    public static Result<T> Failure(List<string> errors) => new(false, default, null, errors);
}

public class Result
{
    public bool IsSuccess { get; private set; }
    public string ErrorMessage { get; private set; }
    public List<string> Errors { get; private set; } = new();

    private Result(bool isSuccess, string errorMessage, List<string> errors = null)
    {
        IsSuccess = isSuccess;
        ErrorMessage = errorMessage;
        Errors = errors ?? new List<string>();
    }

    public static Result Success() => new(true, null);
    public static Result Failure(string errorMessage) => new(false, errorMessage);
    public static Result Failure(List<string> errors) => new(false, null, errors);
}
```

## Deliverables

1. **Commands**: Write operation requests with handlers
2. **Queries**: Read operation requests with handlers
3. **Pipeline Behaviors**: Cross-cutting concern implementations
4. **Result Pattern**: Consistent response handling
5. **Validation Integration**: FluentValidation with MediatR
6. **Logging Integration**: Structured logging throughout
7. **Caching Strategy**: Query result caching
8. **Error Handling**: Comprehensive exception management
9. **Performance Monitoring**: Request timing and optimization
10. **Feature Organization**: Vertical slice architecture

## Validation Checklist

- [ ] CQRS pattern properly implemented
- [ ] Commands and queries separated
- [ ] Pipeline behaviors configured
- [ ] Validation integrated with MediatR
- [ ] Logging implemented throughout
- [ ] Result pattern used consistently
- [ ] Error handling comprehensive
- [ ] Performance monitoring enabled
- [ ] Caching strategy implemented
- [ ] Features organized by business domain