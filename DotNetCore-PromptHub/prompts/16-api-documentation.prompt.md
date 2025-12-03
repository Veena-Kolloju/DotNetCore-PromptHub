---
context: ../design-system/architecture-patterns.md
context: ../instructions/general-coding.instructions.md
---

# API Documentation Framework

Generate comprehensive API documentation using Swagger/OpenAPI with examples, authentication, and interactive testing capabilities.

## Requirements

### 1. Swagger Configuration
- OpenAPI 3.0 specification
- Interactive API explorer
- Authentication integration
- Request/response examples

### 2. Documentation Standards
- Comprehensive endpoint descriptions
- Parameter documentation
- Response schema definitions
- Error code documentation

## Example Implementation

### Swagger Configuration
```csharp
public static class SwaggerConfiguration
{
    public static IServiceCollection AddSwaggerDocumentation(this IServiceCollection services)
    {
        services.AddSwaggerGen(options =>
        {
            options.SwaggerDoc("v1", new OpenApiInfo
            {
                Title = "Customer Management API",
                Version = "v1",
                Description = "A comprehensive API for managing customers, orders, and related business operations",
                Contact = new OpenApiContact
                {
                    Name = "API Support Team",
                    Email = "api-support@company.com",
                    Url = new Uri("https://company.com/support")
                },
                License = new OpenApiLicense
                {
                    Name = "MIT License",
                    Url = new Uri("https://opensource.org/licenses/MIT")
                }
            });

            // JWT Authentication
            options.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
            {
                Description = "JWT Authorization header using the Bearer scheme. Enter 'Bearer' [space] and then your token in the text input below.",
                Name = "Authorization",
                In = ParameterLocation.Header,
                Type = SecuritySchemeType.ApiKey,
                Scheme = "Bearer",
                BearerFormat = "JWT"
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

            // Include XML comments
            var xmlFile = $"{Assembly.GetExecutingAssembly().GetName().Name}.xml";
            var xmlPath = Path.Combine(AppContext.BaseDirectory, xmlFile);
            options.IncludeXmlComments(xmlPath);

            // Custom schema filters
            options.SchemaFilter<EnumSchemaFilter>();
            options.OperationFilter<SwaggerDefaultValues>();
            options.DocumentFilter<SwaggerDocumentFilter>();
        });

        return services;
    }

    public static IApplicationBuilder UseSwaggerDocumentation(this IApplicationBuilder app, IWebHostEnvironment env)
    {
        app.UseSwagger(c =>
        {
            c.RouteTemplate = "api-docs/{documentName}/swagger.json";
        });

        app.UseSwaggerUI(c =>
        {
            c.SwaggerEndpoint("/api-docs/v1/swagger.json", "Customer Management API V1");
            c.RoutePrefix = "api-docs";
            c.DocumentTitle = "Customer Management API Documentation";
            c.DefaultModelsExpandDepth(2);
            c.DefaultModelRendering(ModelRendering.Example);
            c.DisplayRequestDuration();
            c.EnableDeepLinking();
            c.EnableFilter();
            c.ShowExtensions();
        });

        return app;
    }
}
```

### Documented Controller Example
```csharp
/// <summary>
/// Customer management operations
/// </summary>
[ApiController]
[Route("api/v{version:apiVersion}/[controller]")]
[ApiVersion("1.0")]
[Produces("application/json")]
[Consumes("application/json")]
public class CustomersController : ControllerBase
{
    private readonly IMediator _mediator;

    public CustomersController(IMediator mediator)
    {
        _mediator = mediator;
    }

    /// <summary>
    /// Retrieves a paginated list of customers
    /// </summary>
    /// <param name="pageNumber">The page number to retrieve (default: 1)</param>
    /// <param name="pageSize">The number of items per page (default: 10, max: 100)</param>
    /// <param name="searchTerm">Optional search term to filter customers by name or email</param>
    /// <param name="customerType">Optional filter by customer type</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>A paginated list of customers</returns>
    /// <response code="200">Returns the paginated list of customers</response>
    /// <response code="400">If the request parameters are invalid</response>
    /// <response code="401">If the user is not authenticated</response>
    /// <response code="403">If the user doesn't have permission to view customers</response>
    [HttpGet]
    [ProducesResponseType(typeof(ApiResponse<PagedResult<CustomerSummaryDto>>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<ApiResponse<PagedResult<CustomerSummaryDto>>>> GetCustomers(
        [FromQuery] int pageNumber = 1,
        [FromQuery] int pageSize = 10,
        [FromQuery] string searchTerm = null,
        [FromQuery] CustomerType? customerType = null,
        CancellationToken cancellationToken = default)
    {
        var query = new GetCustomersQuery
        {
            PageNumber = pageNumber,
            PageSize = Math.Min(pageSize, 100), // Enforce max page size
            SearchTerm = searchTerm,
            CustomerType = customerType
        };

        var result = await _mediator.Send(query, cancellationToken);
        return Ok(new ApiResponse<PagedResult<CustomerSummaryDto>>
        {
            Success = true,
            Data = result,
            Message = "Customers retrieved successfully"
        });
    }

    /// <summary>
    /// Retrieves a specific customer by ID
    /// </summary>
    /// <param name="id">The unique identifier of the customer</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>The customer details</returns>
    /// <response code="200">Returns the customer details</response>
    /// <response code="404">If the customer is not found</response>
    /// <response code="401">If the user is not authenticated</response>
    [HttpGet("{id:int}")]
    [ProducesResponseType(typeof(ApiResponse<CustomerDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<ApiResponse<CustomerDto>>> GetCustomer(
        [FromRoute] int id,
        CancellationToken cancellationToken = default)
    {
        var query = new GetCustomerByIdQuery(id);
        var result = await _mediator.Send(query, cancellationToken);

        if (result == null)
        {
            return NotFound(new ApiResponse
            {
                Success = false,
                Message = $"Customer with ID {id} not found"
            });
        }

        return Ok(new ApiResponse<CustomerDto>
        {
            Success = true,
            Data = result,
            Message = "Customer retrieved successfully"
        });
    }

    /// <summary>
    /// Creates a new customer
    /// </summary>
    /// <param name="request">The customer creation request</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>The created customer</returns>
    /// <response code="201">Returns the newly created customer</response>
    /// <response code="400">If the request data is invalid</response>
    /// <response code="409">If a customer with the same email already exists</response>
    /// <response code="401">If the user is not authenticated</response>
    /// <response code="403">If the user doesn't have permission to create customers</response>
    [HttpPost]
    [ProducesResponseType(typeof(ApiResponse<CustomerDto>), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status409Conflict)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status403Forbidden)]
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

        return CreatedAtAction(
            nameof(GetCustomer),
            new { id = result.Id },
            new ApiResponse<CustomerDto>
            {
                Success = true,
                Data = result,
                Message = "Customer created successfully"
            });
    }
}
```

### DTO Documentation
```csharp
/// <summary>
/// Customer creation request
/// </summary>
public class CreateCustomerRequest
{
    /// <summary>
    /// The customer's full name
    /// </summary>
    /// <example>John Doe</example>
    [Required]
    [StringLength(100, MinimumLength = 2)]
    public string Name { get; set; }

    /// <summary>
    /// The customer's email address
    /// </summary>
    /// <example>john.doe@example.com</example>
    [Required]
    [EmailAddress]
    public string Email { get; set; }

    /// <summary>
    /// The customer's phone number in international format
    /// </summary>
    /// <example>+1-555-123-4567</example>
    [Phone]
    public string Phone { get; set; }

    /// <summary>
    /// The customer's address information
    /// </summary>
    public AddressDto Address { get; set; }
}

/// <summary>
/// Customer information response
/// </summary>
public class CustomerDto
{
    /// <summary>
    /// Unique identifier for the customer
    /// </summary>
    /// <example>12345</example>
    public int Id { get; set; }

    /// <summary>
    /// The customer's full name
    /// </summary>
    /// <example>John Doe</example>
    public string Name { get; set; }

    /// <summary>
    /// The customer's email address
    /// </summary>
    /// <example>john.doe@example.com</example>
    public string Email { get; set; }

    /// <summary>
    /// The customer's phone number
    /// </summary>
    /// <example>+1-555-123-4567</example>
    public string Phone { get; set; }

    /// <summary>
    /// The customer type indicating their status level
    /// </summary>
    /// <example>Premium</example>
    public CustomerType Type { get; set; }

    /// <summary>
    /// The customer's current status
    /// </summary>
    /// <example>Active</example>
    public CustomerStatus Status { get; set; }

    /// <summary>
    /// The customer's address information
    /// </summary>
    public AddressDto Address { get; set; }

    /// <summary>
    /// When the customer record was created
    /// </summary>
    /// <example>2023-01-15T10:30:00Z</example>
    public DateTime CreatedAt { get; set; }

    /// <summary>
    /// When the customer record was last updated
    /// </summary>
    /// <example>2023-06-20T14:45:00Z</example>
    public DateTime? UpdatedAt { get; set; }
}

/// <summary>
/// Address information
/// </summary>
public class AddressDto
{
    /// <summary>
    /// Street address including house number
    /// </summary>
    /// <example>123 Main Street, Apt 4B</example>
    [Required]
    [StringLength(200)]
    public string Street { get; set; }

    /// <summary>
    /// City name
    /// </summary>
    /// <example>New York</example>
    [Required]
    [StringLength(100)]
    public string City { get; set; }

    /// <summary>
    /// State or province
    /// </summary>
    /// <example>NY</example>
    [Required]
    [StringLength(50)]
    public string State { get; set; }

    /// <summary>
    /// Postal or ZIP code
    /// </summary>
    /// <example>10001</example>
    [Required]
    [StringLength(20)]
    public string ZipCode { get; set; }

    /// <summary>
    /// Country name
    /// </summary>
    /// <example>United States</example>
    [Required]
    [StringLength(100)]
    public string Country { get; set; }
}
```

### Custom Swagger Filters
```csharp
public class EnumSchemaFilter : ISchemaFilter
{
    public void Apply(OpenApiSchema schema, SchemaFilterContext context)
    {
        if (context.Type.IsEnum)
        {
            schema.Enum.Clear();
            Enum.GetNames(context.Type)
                .ToList()
                .ForEach(name => schema.Enum.Add(new OpenApiString(name)));
        }
    }
}

public class SwaggerDefaultValues : IOperationFilter
{
    public void Apply(OpenApiOperation operation, OperationFilterContext context)
    {
        var apiDescription = context.ApiDescription;

        operation.Deprecated |= apiDescription.IsDeprecated();

        foreach (var responseType in context.ApiDescription.SupportedResponseTypes)
        {
            var responseKey = responseType.IsDefaultResponse ? "default" : responseType.StatusCode.ToString();
            var response = operation.Responses[responseKey];

            foreach (var contentType in response.Content.Keys)
            {
                if (responseType.ApiResponseFormats.All(x => x.MediaType != contentType))
                {
                    response.Content.Remove(contentType);
                }
            }
        }

        if (operation.Parameters == null)
            return;

        foreach (var parameter in operation.Parameters)
        {
            var description = apiDescription.ParameterDescriptions.First(p => p.Name == parameter.Name);

            if (parameter.Description == null)
            {
                parameter.Description = description.ModelMetadata?.Description;
            }

            if (parameter.Schema.Default == null && description.DefaultValue != null)
            {
                parameter.Schema.Default = OpenApiAnyFactory.CreateFromJson(JsonSerializer.Serialize(description.DefaultValue));
            }

            parameter.Required |= description.IsRequired;
        }
    }
}

public class SwaggerDocumentFilter : IDocumentFilter
{
    public void Apply(OpenApiDocument swaggerDoc, DocumentFilterContext context)
    {
        var tags = new List<OpenApiTag>
        {
            new OpenApiTag
            {
                Name = "Customers",
                Description = "Operations related to customer management"
            },
            new OpenApiTag
            {
                Name = "Orders",
                Description = "Operations related to order processing"
            },
            new OpenApiTag
            {
                Name = "Authentication",
                Description = "Authentication and authorization operations"
            }
        };

        swaggerDoc.Tags = tags;
    }
}
```

## Deliverables

1. **Swagger Configuration**: Complete OpenAPI setup
2. **Controller Documentation**: Comprehensive endpoint documentation
3. **DTO Documentation**: Request/response model documentation
4. **Authentication Integration**: JWT authentication in Swagger UI
5. **Custom Filters**: Schema and operation customization
6. **Examples**: Request/response examples
7. **Error Documentation**: HTTP status code documentation
8. **Interactive Testing**: Swagger UI for API testing
9. **Export Options**: OpenAPI specification export
10. **Versioning Support**: API version documentation

## Validation Checklist

- [ ] All endpoints documented with descriptions
- [ ] Request/response models include examples
- [ ] Authentication properly integrated
- [ ] HTTP status codes documented
- [ ] Parameter validation documented
- [ ] Interactive testing works correctly
- [ ] OpenAPI specification validates
- [ ] Custom filters enhance documentation
- [ ] Error responses documented
- [ ] API versioning supported