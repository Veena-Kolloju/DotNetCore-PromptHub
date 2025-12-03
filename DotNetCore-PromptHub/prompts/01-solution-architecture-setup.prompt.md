---
context: ../design-system/architecture-patterns.md
context: ../instructions/general-coding.instructions.md
context: ../instructions/guardrails.md
---

# Solution & Architecture Setup

Create a .NET Core solution using Clean Architecture with proper project structure, CQRS implementation, and dependency injection setup.

## Requirements

### 1. Solution Structure
Generate a complete solution with the following projects:
- **Domain**: Core business entities and interfaces
- **Application**: Use cases, commands, queries, and handlers
- **Infrastructure**: Data access and external service implementations
- **API**: Controllers, middleware, and configuration

### 2. CQRS Implementation
- Command/Query separation with MediatR
- Request/Response DTOs for all operations
- Validation pipeline using FluentValidation
- Exception handling pipeline

### 3. Dependency Injection Setup
- Service registration extensions
- Environment-based configuration
- Logging configuration with Serilog
- Health checks implementation

## Example Implementation

### Solution File Structure
```
YourProject.sln
├── src/
│   ├── YourProject.Domain/
│   │   ├── Entities/
│   │   ├── ValueObjects/
│   │   ├── Enums/
│   │   ├── Interfaces/
│   │   └── Events/
│   ├── YourProject.Application/
│   │   ├── Commands/
│   │   ├── Queries/
│   │   ├── DTOs/
│   │   ├── Handlers/
│   │   ├── Validators/
│   │   ├── Mappings/
│   │   └── Interfaces/
│   ├── YourProject.Infrastructure/
│   │   ├── Data/
│   │   ├── Repositories/
│   │   ├── Services/
│   │   └── Configurations/
│   └── YourProject.API/
│       ├── Controllers/
│       ├── Middleware/
│       ├── Extensions/
│       └── Filters/
└── tests/
    ├── YourProject.UnitTests/
    ├── YourProject.IntegrationTests/
    └── YourProject.ArchitectureTests/
```

### Program.cs Configuration
```csharp
var builder = WebApplication.CreateBuilder(args);

// Add services
builder.Services.AddApplicationServices();
builder.Services.AddInfrastructureServices(builder.Configuration);
builder.Services.AddApiServices();

var app = builder.Build();

// Configure pipeline
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();
app.UseAuthentication();
app.UseAuthorization();
app.UseMiddleware<ExceptionHandlingMiddleware>();
app.MapControllers();
app.MapHealthChecks("/health");

app.Run();
```

### Service Registration Extensions
```csharp
// Application Layer
public static class ApplicationServiceExtensions
{
    public static IServiceCollection AddApplicationServices(this IServiceCollection services)
    {
        services.AddMediatR(cfg => cfg.RegisterServicesFromAssembly(Assembly.GetExecutingAssembly()));
        services.AddValidatorsFromAssembly(Assembly.GetExecutingAssembly());
        services.AddAutoMapper(Assembly.GetExecutingAssembly());
        
        services.AddTransient(typeof(IPipelineBehavior<,>), typeof(ValidationBehavior<,>));
        services.AddTransient(typeof(IPipelineBehavior<,>), typeof(LoggingBehavior<,>));
        
        return services;
    }
}

// Infrastructure Layer
public static class InfrastructureServiceExtensions
{
    public static IServiceCollection AddInfrastructureServices(
        this IServiceCollection services, 
        IConfiguration configuration)
    {
        services.AddDbContext<ApplicationDbContext>(options =>
            options.UseSqlServer(configuration.GetConnectionString("DefaultConnection")));
        
        services.AddScoped(typeof(IRepository<>), typeof(Repository<>));
        services.AddScoped<IUnitOfWork, UnitOfWork>();
        
        return services;
    }
}
```

### CQRS Command Example
```csharp
// Command
public record CreateCustomerCommand(
    string Name,
    string Email,
    string Phone) : IRequest<CustomerDto>;

// Handler
public class CreateCustomerHandler : IRequestHandler<CreateCustomerCommand, CustomerDto>
{
    private readonly IRepository<Customer> _repository;
    private readonly IMapper _mapper;
    
    public CreateCustomerHandler(IRepository<Customer> repository, IMapper mapper)
    {
        _repository = repository;
        _mapper = mapper;
    }
    
    public async Task<CustomerDto> Handle(CreateCustomerCommand request, CancellationToken cancellationToken)
    {
        var customer = new Customer(request.Name, request.Email, request.Phone);
        
        await _repository.AddAsync(customer, cancellationToken);
        
        return _mapper.Map<CustomerDto>(customer);
    }
}

// Validator
public class CreateCustomerValidator : AbstractValidator<CreateCustomerCommand>
{
    public CreateCustomerValidator()
    {
        RuleFor(x => x.Name)
            .NotEmpty()
            .MaximumLength(100);
            
        RuleFor(x => x.Email)
            .NotEmpty()
            .EmailAddress();
            
        RuleFor(x => x.Phone)
            .NotEmpty()
            .Matches(@"^\+?[1-9]\d{1,14}$");
    }
}
```

## Deliverables

Generate the following components:

1. **Solution Structure**: Complete project hierarchy with proper references
2. **Domain Entities**: Base entity class and sample entities
3. **CQRS Setup**: MediatR configuration with pipeline behaviors
4. **Repository Pattern**: Generic repository with Unit of Work
5. **API Configuration**: Controllers, middleware, and service registration
6. **Validation Pipeline**: FluentValidation integration with MediatR
7. **Exception Handling**: Global exception middleware
8. **Logging Setup**: Structured logging with Serilog
9. **Health Checks**: Basic health check endpoints
10. **Configuration Management**: Strongly typed configuration classes

## Validation Checklist

- [ ] Clean Architecture layers properly separated
- [ ] CQRS pattern implemented with MediatR
- [ ] Dependency injection configured for all layers
- [ ] Validation pipeline integrated
- [ ] Exception handling middleware implemented
- [ ] Logging configured with correlation IDs
- [ ] Health checks endpoints available
- [ ] Configuration management setup
- [ ] Unit test projects created
- [ ] Architecture tests implemented