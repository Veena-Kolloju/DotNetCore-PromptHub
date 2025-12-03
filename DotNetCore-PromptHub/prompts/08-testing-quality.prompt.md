---
context: ../design-system/architecture-patterns.md
context: ../instructions/general-coding.instructions.md
---

# Testing & Quality Assurance

Generate comprehensive unit tests, integration tests, and quality assurance frameworks for .NET Core applications using xUnit, Moq, and FluentAssertions.

## Requirements

### 1. Unit Testing Framework
- xUnit test framework with proper test organization
- Moq for mocking dependencies
- FluentAssertions for readable assertions
- Test data builders and object mothers

### 2. Integration Testing
- WebApplicationFactory for API testing
- In-memory database for data layer tests
- Test containers for external dependencies
- End-to-end scenario testing

### 3. Test Organization
- Arrange-Act-Assert pattern
- Descriptive test naming conventions
- Test categories and traits
- Shared test utilities and fixtures

## Example Implementation

### Unit Test Base Classes
```csharp
public abstract class UnitTestBase
{
    protected readonly IFixture _fixture;
    protected readonly Mock<ILogger> _mockLogger;

    protected UnitTestBase()
    {
        _fixture = new Fixture();
        _fixture.Behaviors.OfType<ThrowingRecursionBehavior>().ToList()
            .ForEach(b => _fixture.Behaviors.Remove(b));
        _fixture.Behaviors.Add(new OmitOnRecursionBehavior());

        _mockLogger = new Mock<ILogger>();
    }

    protected T CreateMock<T>() where T : class
    {
        return _fixture.Create<T>();
    }

    protected Mock<T> CreateMockObject<T>() where T : class
    {
        return new Mock<T>();
    }
}

// Test Data Builders
public class CustomerBuilder
{
    private string _name = "John Doe";
    private string _email = "john.doe@example.com";
    private string _phone = "+1234567890";
    private CustomerType _type = CustomerType.Regular;
    private CustomerStatus _status = CustomerStatus.Active;
    private Address _address;

    public CustomerBuilder WithName(string name)
    {
        _name = name;
        return this;
    }

    public CustomerBuilder WithEmail(string email)
    {
        _email = email;
        return this;
    }

    public CustomerBuilder WithType(CustomerType type)
    {
        _type = type;
        return this;
    }

    public CustomerBuilder WithVipStatus()
    {
        _type = CustomerType.Vip;
        return this;
    }

    public CustomerBuilder WithAddress(Address address)
    {
        _address = address;
        return this;
    }

    public Customer Build()
    {
        var customer = new Customer(_name, new Email(_email), new PhoneNumber(_phone));
        
        if (_address != null)
        {
            customer.UpdateAddress(_address);
        }

        if (_type == CustomerType.Vip)
        {
            customer.PromoteToVip();
        }

        return customer;
    }

    public static implicit operator Customer(CustomerBuilder builder) => builder.Build();
}
```

### Domain Entity Tests
```csharp
public class CustomerTests : UnitTestBase
{
    [Fact]
    [Trait("Category", "Unit")]
    public void Constructor_WithValidParameters_ShouldCreateCustomer()
    {
        // Arrange
        var name = "John Doe";
        var email = new Email("john@example.com");
        var phone = new PhoneNumber("+1234567890");

        // Act
        var customer = new Customer(name, email, phone);

        // Assert
        customer.Name.Should().Be(name);
        customer.Email.Should().Be(email);
        customer.Phone.Should().Be(phone);
        customer.Type.Should().Be(CustomerType.Regular);
        customer.Status.Should().Be(CustomerStatus.Active);
        customer.CreatedAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(1));
    }

    [Theory]
    [Trait("Category", "Unit")]
    [InlineData("")]
    [InlineData(" ")]
    [InlineData(null)]
    public void Constructor_WithInvalidName_ShouldThrowArgumentException(string invalidName)
    {
        // Arrange
        var email = new Email("john@example.com");
        var phone = new PhoneNumber("+1234567890");

        // Act & Assert
        var exception = Assert.Throws<ArgumentException>(() => new Customer(invalidName, email, phone));
        exception.Message.Should().Contain("name");
    }

    [Fact]
    [Trait("Category", "Unit")]
    public void PromoteToVip_WhenRegularCustomer_ShouldChangeTypeToVip()
    {
        // Arrange
        var customer = new CustomerBuilder()
            .WithType(CustomerType.Regular)
            .Build();

        // Act
        customer.PromoteToVip();

        // Assert
        customer.Type.Should().Be(CustomerType.Vip);
        customer.UpdatedAt.Should().NotBeNull();
    }

    [Fact]
    [Trait("Category", "Unit")]
    public void PromoteToVip_WhenAlreadyVip_ShouldThrowDomainException()
    {
        // Arrange
        var customer = new CustomerBuilder()
            .WithVipStatus()
            .Build();

        // Act & Assert
        var exception = Assert.Throws<DomainException>(() => customer.PromoteToVip());
        exception.Message.Should().Contain("already VIP");
    }

    [Theory]
    [Trait("Category", "Unit")]
    [InlineData(CustomerType.Regular, 1000, 0)]
    [InlineData(CustomerType.Premium, 1000, 100)]
    [InlineData(CustomerType.Vip, 1000, 150)]
    public void CalculateDiscount_WithDifferentTypes_ShouldReturnCorrectDiscount(
        CustomerType type, decimal amount, decimal expectedDiscount)
    {
        // Arrange
        var customer = new CustomerBuilder()
            .WithType(type)
            .Build();

        // Act
        var discount = customer.CalculateDiscount(amount);

        // Assert
        discount.Should().Be(expectedDiscount);
    }
}
```

### Handler Tests
```csharp
public class CreateCustomerHandlerTests : UnitTestBase
{
    private readonly Mock<IUnitOfWork> _mockUnitOfWork;
    private readonly Mock<ICustomerRepository> _mockCustomerRepository;
    private readonly Mock<IMapper> _mockMapper;
    private readonly Mock<IEventBus> _mockEventBus;
    private readonly CreateCustomerHandler _handler;

    public CreateCustomerHandlerTests()
    {
        _mockUnitOfWork = CreateMockObject<IUnitOfWork>();
        _mockCustomerRepository = CreateMockObject<ICustomerRepository>();
        _mockMapper = CreateMockObject<IMapper>();
        _mockEventBus = CreateMockObject<IEventBus>();

        _mockUnitOfWork.Setup(u => u.Customers).Returns(_mockCustomerRepository.Object);

        _handler = new CreateCustomerHandler(
            _mockUnitOfWork.Object,
            _mockMapper.Object,
            _mockEventBus.Object,
            _mockLogger.Object as ILogger<CreateCustomerHandler>);
    }

    [Fact]
    [Trait("Category", "Unit")]
    public async Task Handle_WithValidCommand_ShouldCreateCustomerAndReturnSuccess()
    {
        // Arrange
        var command = new CreateCustomerCommand
        {
            Name = "John Doe",
            Email = "john@example.com",
            Phone = "+1234567890",
            Address = new AddressDto
            {
                Street = "123 Main St",
                City = "Anytown",
                State = "CA",
                ZipCode = "12345",
                Country = "USA"
            }
        };

        var customer = new CustomerBuilder()
            .WithName(command.Name)
            .WithEmail(command.Email)
            .Build();

        var customerDto = new CustomerDto
        {
            Id = 1,
            Name = command.Name,
            Email = command.Email
        };

        _mockCustomerRepository
            .Setup(r => r.GetByEmailAsync(command.Email, It.IsAny<CancellationToken>()))
            .ReturnsAsync((Customer)null);

        _mockMapper
            .Setup(m => m.Map<CustomerDto>(It.IsAny<Customer>()))
            .Returns(customerDto);

        _mockUnitOfWork
            .Setup(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(1);

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Data.Should().NotBeNull();
        result.Data.Name.Should().Be(command.Name);
        result.Data.Email.Should().Be(command.Email);

        _mockCustomerRepository.Verify(r => r.AddAsync(It.IsAny<Customer>(), It.IsAny<CancellationToken>()), Times.Once);
        _mockUnitOfWork.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
        _mockEventBus.Verify(e => e.PublishAsync(It.IsAny<CustomerCreatedEvent>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    [Trait("Category", "Unit")]
    public async Task Handle_WithExistingEmail_ShouldReturnFailure()
    {
        // Arrange
        var command = new CreateCustomerCommand
        {
            Name = "John Doe",
            Email = "john@example.com",
            Phone = "+1234567890"
        };

        var existingCustomer = new CustomerBuilder()
            .WithEmail(command.Email)
            .Build();

        _mockCustomerRepository
            .Setup(r => r.GetByEmailAsync(command.Email, It.IsAny<CancellationToken>()))
            .ReturnsAsync(existingCustomer);

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.ErrorMessage.Should().Contain("already exists");

        _mockCustomerRepository.Verify(r => r.AddAsync(It.IsAny<Customer>(), It.IsAny<CancellationToken>()), Times.Never);
        _mockUnitOfWork.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    [Trait("Category", "Unit")]
    public async Task Handle_WhenRepositoryThrows_ShouldReturnFailure()
    {
        // Arrange
        var command = new CreateCustomerCommand
        {
            Name = "John Doe",
            Email = "john@example.com",
            Phone = "+1234567890"
        };

        _mockCustomerRepository
            .Setup(r => r.GetByEmailAsync(command.Email, It.IsAny<CancellationToken>()))
            .ReturnsAsync((Customer)null);

        _mockCustomerRepository
            .Setup(r => r.AddAsync(It.IsAny<Customer>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new Exception("Database error"));

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.ErrorMessage.Should().Contain("error occurred");
    }
}
```

### Controller Tests
```csharp
public class CustomersControllerTests : UnitTestBase
{
    private readonly Mock<IMediator> _mockMediator;
    private readonly CustomersController _controller;

    public CustomersControllerTests()
    {
        _mockMediator = CreateMockObject<IMediator>();
        _controller = new CustomersController(_mockMediator.Object, _mockLogger.Object as ILogger<CustomersController>);
    }

    [Fact]
    [Trait("Category", "Unit")]
    public async Task GetCustomer_WithValidId_ShouldReturnOkResult()
    {
        // Arrange
        var customerId = 1;
        var customerDto = new CustomerDto
        {
            Id = customerId,
            Name = "John Doe",
            Email = "john@example.com"
        };

        var queryResult = Result<CustomerDto>.Success(customerDto);

        _mockMediator
            .Setup(m => m.Send(It.Is<GetCustomerByIdQuery>(q => q.Id == customerId), It.IsAny<CancellationToken>()))
            .ReturnsAsync(queryResult);

        // Act
        var result = await _controller.GetCustomer(customerId, CancellationToken.None);

        // Assert
        var okResult = result.Result.Should().BeOfType<OkObjectResult>().Subject;
        var response = okResult.Value.Should().BeOfType<ApiResponse<CustomerDto>>().Subject;

        response.Success.Should().BeTrue();
        response.Data.Should().BeEquivalentTo(customerDto);

        _mockMediator.Verify(m => m.Send(It.IsAny<GetCustomerByIdQuery>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    [Trait("Category", "Unit")]
    public async Task GetCustomer_WithInvalidId_ShouldReturnNotFound()
    {
        // Arrange
        var customerId = 999;
        var queryResult = Result<CustomerDto>.Failure("Customer not found");

        _mockMediator
            .Setup(m => m.Send(It.Is<GetCustomerByIdQuery>(q => q.Id == customerId), It.IsAny<CancellationToken>()))
            .ReturnsAsync(queryResult);

        // Act
        var result = await _controller.GetCustomer(customerId, CancellationToken.None);

        // Assert
        var notFoundResult = result.Result.Should().BeOfType<NotFoundObjectResult>().Subject;
        var response = notFoundResult.Value.Should().BeOfType<ApiResponse>().Subject;

        response.Success.Should().BeFalse();
        response.Message.Should().Contain("not found");
    }

    [Fact]
    [Trait("Category", "Unit")]
    public async Task CreateCustomer_WithValidRequest_ShouldReturnCreatedResult()
    {
        // Arrange
        var request = new CreateCustomerRequest
        {
            Name = "John Doe",
            Email = "john@example.com",
            Phone = "+1234567890"
        };

        var customerDto = new CustomerDto
        {
            Id = 1,
            Name = request.Name,
            Email = request.Email
        };

        var commandResult = Result<CustomerDto>.Success(customerDto);

        _mockMediator
            .Setup(m => m.Send(It.IsAny<CreateCustomerCommand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(commandResult);

        // Act
        var result = await _controller.CreateCustomer(request, CancellationToken.None);

        // Assert
        var createdResult = result.Result.Should().BeOfType<CreatedAtActionResult>().Subject;
        var response = createdResult.Value.Should().BeOfType<ApiResponse<CustomerDto>>().Subject;

        response.Success.Should().BeTrue();
        response.Data.Should().BeEquivalentTo(customerDto);

        createdResult.ActionName.Should().Be(nameof(CustomersController.GetCustomer));
        createdResult.RouteValues["id"].Should().Be(customerDto.Id);
    }
}
```

### Integration Tests
```csharp
public class CustomersIntegrationTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly WebApplicationFactory<Program> _factory;
    private readonly HttpClient _client;

    public CustomersIntegrationTests(WebApplicationFactory<Program> factory)
    {
        _factory = factory.WithWebHostBuilder(builder =>
        {
            builder.ConfigureServices(services =>
            {
                // Remove the real database context
                var descriptor = services.SingleOrDefault(d => d.ServiceType == typeof(DbContextOptions<ApplicationDbContext>));
                if (descriptor != null)
                    services.Remove(descriptor);

                // Add in-memory database
                services.AddDbContext<ApplicationDbContext>(options =>
                    options.UseInMemoryDatabase("TestDb"));

                // Replace external services with mocks
                services.AddScoped<IEmailService, MockEmailService>();
                services.AddScoped<IPaymentService, MockPaymentService>();
            });
        });

        _client = _factory.CreateClient();
    }

    [Fact]
    [Trait("Category", "Integration")]
    public async Task GetCustomers_ShouldReturnPagedResults()
    {
        // Arrange
        await SeedTestDataAsync();

        // Act
        var response = await _client.GetAsync("/api/v1/customers?pageNumber=1&pageSize=10");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var content = await response.Content.ReadAsStringAsync();
        var apiResponse = JsonSerializer.Deserialize<ApiResponse<PagedResult<CustomerSummaryDto>>>(content, new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        });

        apiResponse.Success.Should().BeTrue();
        apiResponse.Data.Items.Should().NotBeEmpty();
        apiResponse.Data.TotalCount.Should().BeGreaterThan(0);
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
            Phone = "+1234567890",
            Address = new AddressDto
            {
                Street = "123 Test St",
                City = "Test City",
                State = "TS",
                ZipCode = "12345",
                Country = "Test Country"
            }
        };

        var json = JsonSerializer.Serialize(request, new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        });
        var content = new StringContent(json, Encoding.UTF8, "application/json");

        // Act
        var response = await _client.PostAsync("/api/v1/customers", content);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Created);

        var responseContent = await response.Content.ReadAsStringAsync();
        var apiResponse = JsonSerializer.Deserialize<ApiResponse<CustomerDto>>(responseContent, new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        });

        apiResponse.Success.Should().BeTrue();
        apiResponse.Data.Name.Should().Be(request.Name);
        apiResponse.Data.Email.Should().Be(request.Email);

        response.Headers.Location.Should().NotBeNull();
    }

    private async Task SeedTestDataAsync()
    {
        using var scope = _factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

        if (!context.Customers.Any())
        {
            var customers = new List<Customer>
            {
                new CustomerBuilder().WithName("Test Customer 1").WithEmail("test1@example.com").Build(),
                new CustomerBuilder().WithName("Test Customer 2").WithEmail("test2@example.com").Build(),
                new CustomerBuilder().WithName("Test Customer 3").WithEmail("test3@example.com").Build()
            };

            context.Customers.AddRange(customers);
            await context.SaveChangesAsync();
        }
    }
}
```

### Test Utilities and Mocks
```csharp
public class MockEmailService : IEmailService
{
    public Task<bool> SendWelcomeEmailAsync(string email, string name, CancellationToken cancellationToken = default)
    {
        return Task.FromResult(true);
    }

    public Task<bool> IsHealthyAsync(CancellationToken cancellationToken = default)
    {
        return Task.FromResult(true);
    }
}

public class MockPaymentService : IPaymentService
{
    public Task<PaymentResult> ProcessPaymentAsync(PaymentRequest request, CancellationToken cancellationToken = default)
    {
        return Task.FromResult(new PaymentResult
        {
            IsSuccess = true,
            PaymentId = Guid.NewGuid().ToString(),
            TransactionId = "MOCK_TRANSACTION_123"
        });
    }

    public Task<PaymentStatus> GetPaymentStatusAsync(string paymentId, CancellationToken cancellationToken = default)
    {
        return Task.FromResult(new PaymentStatus
        {
            PaymentId = paymentId,
            Status = "Completed"
        });
    }

    public Task<bool> RefundPaymentAsync(string paymentId, decimal amount, CancellationToken cancellationToken = default)
    {
        return Task.FromResult(true);
    }
}
```

## Deliverables

1. **Unit Test Framework**: xUnit setup with base classes and utilities
2. **Test Data Builders**: Fluent builders for test data creation
3. **Domain Tests**: Comprehensive entity and value object tests
4. **Handler Tests**: CQRS command and query handler tests
5. **Controller Tests**: API endpoint tests with proper mocking
6. **Integration Tests**: End-to-end API testing with test database
7. **Mock Services**: Test doubles for external dependencies
8. **Test Utilities**: Shared testing infrastructure and helpers
9. **Performance Tests**: Load and stress testing scenarios
10. **Architecture Tests**: Dependency and layer validation tests

## Validation Checklist

- [ ] Unit tests cover all business logic
- [ ] Integration tests validate API endpoints
- [ ] Test data builders provide flexible test data
- [ ] Mocking strategy isolates units under test
- [ ] Test naming follows descriptive conventions
- [ ] Test coverage meets minimum requirements (80%+)
- [ ] Performance tests validate response times
- [ ] Architecture tests enforce design rules
- [ ] Test utilities reduce code duplication
- [ ] CI/CD pipeline runs all tests automatically