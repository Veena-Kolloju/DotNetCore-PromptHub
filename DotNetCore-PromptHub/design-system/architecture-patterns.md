# .NET Core Architecture Design System

## Clean Architecture Layers

### Domain Layer (Core)
- **Entities**: Business objects with identity and behavior
- **Value Objects**: Immutable objects without identity
- **Enums**: Domain-specific enumerations
- **Interfaces**: Contracts for external dependencies
- **Events**: Domain events for business notifications

### Application Layer
- **Commands**: Write operations with handlers
- **Queries**: Read operations with handlers
- **DTOs**: Data transfer objects for API contracts
- **Validators**: Input validation using FluentValidation
- **Mappings**: AutoMapper profiles for object mapping

### Infrastructure Layer
- **Repositories**: Data access implementations
- **Services**: External service integrations
- **Configurations**: Entity Framework configurations
- **Migrations**: Database schema changes

### API Layer
- **Controllers**: HTTP endpoint handlers
- **Middleware**: Cross-cutting concerns
- **Filters**: Request/response processing
- **Extensions**: Service registration helpers

## Standard Response Patterns

### API Response Wrapper
```csharp
public class ApiResponse<T>
{
    public bool Success { get; set; }
    public T Data { get; set; }
    public string Message { get; set; }
    public List<string> Errors { get; set; }
    public DateTime Timestamp { get; set; }
}
```

### Pagination Pattern
```csharp
public class PagedResult<T>
{
    public List<T> Items { get; set; }
    public int TotalCount { get; set; }
    public int PageNumber { get; set; }
    public int PageSize { get; set; }
    public int TotalPages => (int)Math.Ceiling((double)TotalCount / PageSize);
}
```

## Naming Conventions

### Projects
- `{CompanyName}.{ProjectName}.Domain`
- `{CompanyName}.{ProjectName}.Application`
- `{CompanyName}.{ProjectName}.Infrastructure`
- `{CompanyName}.{ProjectName}.API`

### Classes
- **Entities**: `Customer`, `Order`, `Product`
- **Commands**: `CreateCustomerCommand`, `UpdateOrderCommand`
- **Queries**: `GetCustomerQuery`, `GetOrdersQuery`
- **Handlers**: `CreateCustomerHandler`, `GetCustomerHandler`
- **DTOs**: `CustomerDto`, `CreateCustomerRequest`

### Methods
- **Commands**: `CreateAsync`, `UpdateAsync`, `DeleteAsync`
- **Queries**: `GetByIdAsync`, `GetAllAsync`, `SearchAsync`
- **Validators**: `ValidateAsync`, `ValidateCommand`