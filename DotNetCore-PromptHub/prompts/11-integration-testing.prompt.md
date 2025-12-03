---
context: ../design-system/architecture-patterns.md
context: ../instructions/general-coding.instructions.md
---

# Integration Testing Framework

Create comprehensive integration tests for .NET Core APIs using WebApplicationFactory and test containers.

## Requirements

### 1. Test Infrastructure
- WebApplicationFactory for API testing
- In-memory database configuration
- Test containers for external services
- Custom test fixtures and collections

### 2. API Testing
- HTTP client testing with authentication
- Request/response validation
- Database state verification
- End-to-end scenario testing

## Example Implementation

### Integration Test Base
```csharp
public class IntegrationTestBase : IClassFixture<WebApplicationFactory<Program>>
{
    protected readonly WebApplicationFactory<Program> _factory;
    protected readonly HttpClient _client;
    protected readonly IServiceScope _scope;
    protected readonly ApplicationDbContext _context;

    public IntegrationTestBase(WebApplicationFactory<Program> factory)
    {
        _factory = factory.WithWebHostBuilder(builder =>
        {
            builder.ConfigureServices(services =>
            {
                var descriptor = services.SingleOrDefault(d => d.ServiceType == typeof(DbContextOptions<ApplicationDbContext>));
                if (descriptor != null) services.Remove(descriptor);

                services.AddDbContext<ApplicationDbContext>(options =>
                    options.UseInMemoryDatabase($"TestDb_{Guid.NewGuid()}"));

                services.AddScoped<IEmailService, MockEmailService>();
            });
        });

        _client = _factory.CreateClient();
        _scope = _factory.Services.CreateScope();
        _context = _scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
    }

    protected async Task<string> GetJwtTokenAsync()
    {
        var loginRequest = new { Email = "test@example.com", Password = "Test123!" };
        var response = await _client.PostAsJsonAsync("/api/auth/login", loginRequest);
        var result = await response.Content.ReadFromJsonAsync<AuthResponse>();
        return result.Token;
    }

    protected async Task SeedDataAsync()
    {
        if (!_context.Customers.Any())
        {
            var customers = new List<Customer>
            {
                new CustomerBuilder().WithName("Test Customer 1").Build(),
                new CustomerBuilder().WithName("Test Customer 2").Build()
            };
            _context.Customers.AddRange(customers);
            await _context.SaveChangesAsync();
        }
    }
}
```

### API Integration Tests
```csharp
public class CustomersApiTests : IntegrationTestBase
{
    public CustomersApiTests(WebApplicationFactory<Program> factory) : base(factory) { }

    [Fact]
    [Trait("Category", "Integration")]
    public async Task GetCustomers_ShouldReturnPagedResults()
    {
        // Arrange
        await SeedDataAsync();
        var token = await GetJwtTokenAsync();
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        // Act
        var response = await _client.GetAsync("/api/v1/customers?pageNumber=1&pageSize=10");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var content = await response.Content.ReadFromJsonAsync<ApiResponse<PagedResult<CustomerDto>>>();
        content.Success.Should().BeTrue();
        content.Data.Items.Should().NotBeEmpty();
    }

    [Fact]
    [Trait("Category", "Integration")]
    public async Task CreateCustomer_WithValidData_ShouldReturnCreated()
    {
        // Arrange
        var request = new CreateCustomerRequest
        {
            Name = "Integration Test Customer",
            Email = "integration@test.com",
            Phone = "+1234567890"
        };

        // Act
        var response = await _client.PostAsJsonAsync("/api/v1/customers", request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Created);
        
        var customer = _context.Customers.FirstOrDefault(c => c.Email.Value == request.Email);
        customer.Should().NotBeNull();
        customer.Name.Should().Be(request.Name);
    }
}
```

### Database Integration Tests
```csharp
public class CustomerRepositoryIntegrationTests : IDisposable
{
    private readonly ApplicationDbContext _context;

    public CustomerRepositoryIntegrationTests()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase($"TestDb_{Guid.NewGuid()}")
            .Options;
        _context = new ApplicationDbContext(options);
    }

    [Fact]
    [Trait("Category", "Integration")]
    public async Task AddAsync_ShouldPersistCustomer()
    {
        // Arrange
        var repository = new CustomerRepository(_context);
        var customer = new CustomerBuilder().Build();

        // Act
        await repository.AddAsync(customer);
        await _context.SaveChangesAsync();

        // Assert
        var savedCustomer = await _context.Customers.FirstOrDefaultAsync(c => c.Id == customer.Id);
        savedCustomer.Should().NotBeNull();
        savedCustomer.Name.Should().Be(customer.Name);
    }

    public void Dispose() => _context?.Dispose();
}
```

## Deliverables

1. **Test Infrastructure**: WebApplicationFactory setup
2. **API Tests**: HTTP endpoint testing
3. **Database Tests**: Repository integration tests
4. **Authentication Tests**: JWT token validation
5. **End-to-End Tests**: Complete user scenarios
6. **Performance Tests**: Load testing scenarios
7. **Test Containers**: External service testing
8. **Mock Services**: Test doubles for dependencies
9. **Test Data**: Seed data and fixtures
10. **CI/CD Integration**: Automated test execution

## Validation Checklist

- [ ] All API endpoints covered
- [ ] Database operations tested
- [ ] Authentication flows validated
- [ ] Error scenarios tested
- [ ] Performance requirements met
- [ ] Test isolation maintained
- [ ] External dependencies mocked
- [ ] Test data properly seeded
- [ ] CI/CD pipeline configured
- [ ] Test reports generated