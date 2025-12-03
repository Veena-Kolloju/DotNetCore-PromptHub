---
context: ../design-system/architecture-patterns.md
context: ../instructions/general-coding.instructions.md
---

# API Layer (Controllers & Endpoints)

Create RESTful controllers and endpoints following best practices with consistent response shapes, proper error handling, and API versioning.

## Requirements

### 1. RESTful Controllers
- Standard HTTP methods (GET, POST, PUT, DELETE)
- Proper HTTP status codes
- Consistent API response format
- Model binding and validation

### 2. Error Handling
- Global exception handling middleware
- Problem Details (RFC 7807) implementation
- Validation error responses
- Structured error logging

### 3. API Features
- API versioning support
- Swagger/OpenAPI documentation
- Rate limiting and throttling
- CORS configuration

## Example Implementation

### Base Controller
```csharp
[ApiController]
[Route("api/v{version:apiVersion}/[controller]")]
[ApiVersion("1.0")]
[Produces("application/json")]
public abstract class BaseApiController : ControllerBase
{
    protected readonly IMediator _mediator;
    protected readonly ILogger _logger;

    protected BaseApiController(IMediator mediator, ILogger logger)
    {
        _mediator = mediator;
        _logger = logger;
    }

    protected ActionResult<ApiResponse<T>> Success<T>(T data, string message = null)
    {
        return Ok(new ApiResponse<T>
        {
            Success = true,
            Data = data,
            Message = message ?? "Operation completed successfully",
            Timestamp = DateTime.UtcNow
        });
    }

    protected ActionResult<ApiResponse> Success(string message = "Operation completed successfully")
    {
        return Ok(new ApiResponse
        {
            Success = true,
            Message = message,
            Timestamp = DateTime.UtcNow
        });
    }

    protected ActionResult<ApiResponse> Error(string message, int statusCode = 400)
    {
        return StatusCode(statusCode, new ApiResponse
        {
            Success = false,
            Message = message,
            Timestamp = DateTime.UtcNow
        });
    }

    protected ActionResult<ApiResponse> ValidationError(List<string> errors)
    {
        return BadRequest(new ApiResponse
        {
            Success = false,
            Message = "Validation failed",
            Errors = errors,
            Timestamp = DateTime.UtcNow
        });
    }

    protected ActionResult HandleResult<T>(Result<T> result)
    {
        if (result.IsSuccess)
        {
            return Success(result.Data);
        }

        if (result.Errors.Any())
        {
            return ValidationError(result.Errors);
        }

        return Error(result.ErrorMessage);
    }
}
```

### Customer Controller Example
```csharp
[ApiController]
[Route("api/v{version:apiVersion}/[controller]")]
[ApiVersion("1.0")]
[Authorize]
public class CustomersController : BaseApiController
{
    public CustomersController(IMediator mediator, ILogger<CustomersController> logger)
        : base(mediator, logger) { }

    /// <summary>
    /// Get all customers with pagination
    /// </summary>
    /// <param name="pageNumber">Page number (default: 1)</param>
    /// <param name="pageSize">Page size (default: 10, max: 100)</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Paginated list of customers</returns>
    [HttpGet]
    [ProducesResponseType(typeof(ApiResponse<PagedResult<CustomerSummaryDto>>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<ApiResponse<PagedResult<CustomerSummaryDto>>>> GetCustomers(
        [FromQuery] int pageNumber = 1,
        [FromQuery] int pageSize = 10,
        CancellationToken cancellationToken = default)
    {
        if (pageSize > 100)
        {
            return Error("Page size cannot exceed 100");
        }

        var query = new GetCustomersQuery(pageNumber, pageSize);
        var result = await _mediator.Send(query, cancellationToken);

        return HandleResult(result);
    }

    /// <summary>
    /// Get customer by ID
    /// </summary>
    /// <param name="id">Customer ID</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Customer details</returns>
    [HttpGet("{id:int}")]
    [ProducesResponseType(typeof(ApiResponse<CustomerDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ApiResponse<CustomerDto>>> GetCustomer(
        int id,
        CancellationToken cancellationToken = default)
    {
        var query = new GetCustomerByIdQuery(id);
        var result = await _mediator.Send(query, cancellationToken);

        if (!result.IsSuccess)
        {
            return NotFound(new ApiResponse
            {
                Success = false,
                Message = result.ErrorMessage,
                Timestamp = DateTime.UtcNow
            });
        }

        return Success(result.Data);
    }

    /// <summary>
    /// Search customers with filters
    /// </summary>
    /// <param name="request">Search criteria</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Filtered and paginated customers</returns>
    [HttpPost("search")]
    [ProducesResponseType(typeof(ApiResponse<PagedResult<CustomerSummaryDto>>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<ApiResponse<PagedResult<CustomerSummaryDto>>>> SearchCustomers(
        [FromBody] SearchCustomersRequest request,
        CancellationToken cancellationToken = default)
    {
        var query = new SearchCustomersQuery
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

        var result = await _mediator.Send(query, cancellationToken);
        return HandleResult(result);
    }

    /// <summary>
    /// Create a new customer
    /// </summary>
    /// <param name="request">Customer creation data</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Created customer</returns>
    [HttpPost]
    [ProducesResponseType(typeof(ApiResponse<CustomerDto>), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<ApiResponse<CustomerDto>>> CreateCustomer(
        [FromBody] CreateCustomerRequest request,
        CancellationToken cancellationToken = default)
    {
        var command = new CreateCustomerCommand
        {
            Name = request.Name,
            Email = request.Email,
            Phone = request.Phone,
            Address = request.Address
        };

        var result = await _mediator.Send(command, cancellationToken);

        if (!result.IsSuccess)
        {
            return HandleResult(result);
        }

        return CreatedAtAction(
            nameof(GetCustomer),
            new { id = result.Data.Id },
            new ApiResponse<CustomerDto>
            {
                Success = true,
                Data = result.Data,
                Message = "Customer created successfully",
                Timestamp = DateTime.UtcNow
            });
    }

    /// <summary>
    /// Update an existing customer
    /// </summary>
    /// <param name="id">Customer ID</param>
    /// <param name="request">Customer update data</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Updated customer</returns>
    [HttpPut("{id:int}")]
    [ProducesResponseType(typeof(ApiResponse<CustomerDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<ApiResponse<CustomerDto>>> UpdateCustomer(
        int id,
        [FromBody] UpdateCustomerRequest request,
        CancellationToken cancellationToken = default)
    {
        var command = new UpdateCustomerCommand
        {
            Id = id,
            Name = request.Name,
            Email = request.Email,
            Phone = request.Phone,
            Address = request.Address
        };

        var result = await _mediator.Send(command, cancellationToken);
        return HandleResult(result);
    }

    /// <summary>
    /// Delete a customer (soft delete)
    /// </summary>
    /// <param name="id">Customer ID</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Success confirmation</returns>
    [HttpDelete("{id:int}")]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ApiResponse>> DeleteCustomer(
        int id,
        CancellationToken cancellationToken = default)
    {
        var command = new DeleteCustomerCommand(id);
        var result = await _mediator.Send(command, cancellationToken);

        if (!result.IsSuccess)
        {
            return NotFound(new ApiResponse
            {
                Success = false,
                Message = result.ErrorMessage,
                Timestamp = DateTime.UtcNow
            });
        }

        return Success("Customer deleted successfully");
    }

    /// <summary>
    /// Promote customer to VIP status
    /// </summary>
    /// <param name="id">Customer ID</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Updated customer</returns>
    [HttpPatch("{id:int}/promote-to-vip")]
    [ProducesResponseType(typeof(ApiResponse<CustomerDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<ApiResponse<CustomerDto>>> PromoteToVip(
        int id,
        CancellationToken cancellationToken = default)
    {
        var command = new PromoteCustomerToVipCommand(id);
        var result = await _mediator.Send(command, cancellationToken);
        return HandleResult(result);
    }
}
```

### Global Exception Handling Middleware
```csharp
public class ExceptionHandlingMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<ExceptionHandlingMiddleware> _logger;

    public ExceptionHandlingMiddleware(RequestDelegate next, ILogger<ExceptionHandlingMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await _next(context);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "An unhandled exception occurred");
            await HandleExceptionAsync(context, ex);
        }
    }

    private static async Task HandleExceptionAsync(HttpContext context, Exception exception)
    {
        var response = context.Response;
        response.ContentType = "application/json";

        var apiResponse = new ApiResponse
        {
            Success = false,
            Timestamp = DateTime.UtcNow
        };

        switch (exception)
        {
            case ValidationException validationEx:
                response.StatusCode = StatusCodes.Status400BadRequest;
                apiResponse.Message = "Validation failed";
                apiResponse.Errors = validationEx.Errors.Select(e => e.ErrorMessage).ToList();
                break;

            case NotFoundException notFoundEx:
                response.StatusCode = StatusCodes.Status404NotFound;
                apiResponse.Message = notFoundEx.Message;
                break;

            case UnauthorizedAccessException:
                response.StatusCode = StatusCodes.Status401Unauthorized;
                apiResponse.Message = "Unauthorized access";
                break;

            case ForbiddenAccessException:
                response.StatusCode = StatusCodes.Status403Forbidden;
                apiResponse.Message = "Forbidden access";
                break;

            case DomainException domainEx:
                response.StatusCode = StatusCodes.Status400BadRequest;
                apiResponse.Message = domainEx.Message;
                break;

            default:
                response.StatusCode = StatusCodes.Status500InternalServerError;
                apiResponse.Message = "An internal server error occurred";
                break;
        }

        var jsonResponse = JsonSerializer.Serialize(apiResponse, new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        });

        await response.WriteAsync(jsonResponse);
    }
}
```

### API Response Models
```csharp
public class ApiResponse
{
    public bool Success { get; set; }
    public string Message { get; set; }
    public List<string> Errors { get; set; } = new();
    public DateTime Timestamp { get; set; }
}

public class ApiResponse<T> : ApiResponse
{
    public T Data { get; set; }
}

public class PagedApiResponse<T> : ApiResponse<PagedResult<T>>
{
    public PaginationMetadata Pagination { get; set; }
}

public class PaginationMetadata
{
    public int CurrentPage { get; set; }
    public int TotalPages { get; set; }
    public int PageSize { get; set; }
    public int TotalCount { get; set; }
    public bool HasPrevious { get; set; }
    public bool HasNext { get; set; }
}
```

### API Configuration
```csharp
public static class ApiServiceExtensions
{
    public static IServiceCollection AddApiServices(this IServiceCollection services)
    {
        services.AddControllers(options =>
        {
            options.Filters.Add<ValidationFilter>();
            options.ModelValidatorProviders.Clear();
        });

        services.AddApiVersioning(options =>
        {
            options.DefaultApiVersion = new ApiVersion(1, 0);
            options.AssumeDefaultVersionWhenUnspecified = true;
            options.ApiVersionReader = ApiVersionReader.Combine(
                new UrlSegmentApiVersionReader(),
                new HeaderApiVersionReader("X-Version"),
                new QueryStringApiVersionReader("version"));
        });

        services.AddVersionedApiExplorer(options =>
        {
            options.GroupNameFormat = "'v'VVV";
            options.SubstituteApiVersionInUrl = true;
        });

        services.AddSwaggerGen(options =>
        {
            options.SwaggerDoc("v1", new OpenApiInfo
            {
                Title = "Customer Management API",
                Version = "v1",
                Description = "A comprehensive API for managing customers"
            });

            options.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
            {
                Description = "JWT Authorization header using the Bearer scheme",
                Name = "Authorization",
                In = ParameterLocation.Header,
                Type = SecuritySchemeType.ApiKey,
                Scheme = "Bearer"
            });

            options.AddSecurityRequirement(new OpenApiSecurityRequirement
            {
                {
                    new OpenApiSecurityScheme
                    {
                        Reference = new OpenApiReference
                        {
                            Type = ReferenceType.SecurityScheme,
                            Id = "Bearer"
                        }
                    },
                    Array.Empty<string>()
                }
            });

            var xmlFile = $"{Assembly.GetExecutingAssembly().GetName().Name}.xml";
            var xmlPath = Path.Combine(AppContext.BaseDirectory, xmlFile);
            options.IncludeXmlComments(xmlPath);
        });

        services.AddCors(options =>
        {
            options.AddDefaultPolicy(builder =>
            {
                builder.AllowAnyOrigin()
                       .AllowAnyMethod()
                       .AllowAnyHeader();
            });
        });

        return services;
    }
}
```

## Deliverables

1. **Base Controller**: Common functionality for all controllers
2. **RESTful Controllers**: CRUD operations with proper HTTP methods
3. **Exception Middleware**: Global error handling
4. **API Response Models**: Consistent response structure
5. **Validation Filters**: Request validation handling
6. **API Versioning**: Version management configuration
7. **Swagger Configuration**: API documentation setup
8. **CORS Configuration**: Cross-origin request handling
9. **Rate Limiting**: Request throttling implementation
10. **Health Check Endpoints**: Application health monitoring

## Validation Checklist

- [ ] RESTful principles followed
- [ ] Proper HTTP status codes used
- [ ] Consistent API response format
- [ ] Global exception handling implemented
- [ ] Validation errors properly handled
- [ ] API versioning configured
- [ ] Swagger documentation complete
- [ ] CORS properly configured
- [ ] Rate limiting implemented
- [ ] Health checks available