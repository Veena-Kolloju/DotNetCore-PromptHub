---
context: ../design-system/architecture-patterns.md
context: ../instructions/general-coding.instructions.md
---

# Unit Test Creation Framework

Generate comprehensive unit tests for .NET Core applications with proper mocking, assertions, and test organization.

## Requirements

### 1. Test Structure
- Arrange-Act-Assert pattern
- Descriptive test method names
- Test categories and traits
- Parameterized tests with Theory/InlineData

### 2. Mocking Strategy
- Mock external dependencies
- Verify method calls and interactions
- Setup return values and exceptions
- Callback verification

## Example Implementation

### Test Base Class
```csharp
public abstract class TestBase
{
    protected readonly IFixture _fixture;
    protected readonly Mock<ILogger> _mockLogger;

    protected TestBase()
    {
        _fixture = new Fixture();
        _fixture.Behaviors.Add(new OmitOnRecursionBehavior());
        _mockLogger = new Mock<ILogger>();
    }

    protected Mock<T> CreateMock<T>() where T : class => new Mock<T>();
}
```

### Domain Entity Tests
```csharp
public class CustomerTests : TestBase
{
    [Fact]
    [Trait("Category", "Unit")]
    public void Constructor_WithValidData_ShouldCreateCustomer()
    {
        // Arrange
        var name = "John Doe";
        var email = new Email("john@test.com");
        var phone = new PhoneNumber("+1234567890");

        // Act
        var customer = new Customer(name, email, phone);

        // Assert
        customer.Name.Should().Be(name);
        customer.Email.Should().Be(email);
        customer.Phone.Should().Be(phone);
        customer.Type.Should().Be(CustomerType.Regular);
    }

    [Theory]
    [Trait("Category", "Unit")]
    [InlineData(CustomerType.Regular, 1000, 0)]
    [InlineData(CustomerType.Premium, 1000, 100)]
    [InlineData(CustomerType.Vip, 1000, 150)]
    public void CalculateDiscount_WithDifferentTypes_ShouldReturnCorrectAmount(
        CustomerType type, decimal amount, decimal expected)
    {
        // Arrange
        var customer = new CustomerBuilder().WithType(type).Build();

        // Act
        var discount = customer.CalculateDiscount(amount);

        // Assert
        discount.Should().Be(expected);
    }
}
```

### Service Tests with Mocking
```csharp
public class CustomerServiceTests : TestBase
{
    private readonly Mock<ICustomerRepository> _mockRepository;
    private readonly Mock<IMapper> _mockMapper;
    private readonly CustomerService _service;

    public CustomerServiceTests()
    {
        _mockRepository = CreateMock<ICustomerRepository>();
        _mockMapper = CreateMock<IMapper>();
        _service = new CustomerService(_mockRepository.Object, _mockMapper.Object);
    }

    [Fact]
    [Trait("Category", "Unit")]
    public async Task CreateAsync_WithValidData_ShouldReturnSuccess()
    {
        // Arrange
        var request = _fixture.Create<CreateCustomerRequest>();
        var customer = new CustomerBuilder().Build();
        var customerDto = _fixture.Create<CustomerDto>();

        _mockRepository
            .Setup(r => r.GetByEmailAsync(request.Email, It.IsAny<CancellationToken>()))
            .ReturnsAsync((Customer)null);

        _mockMapper
            .Setup(m => m.Map<CustomerDto>(It.IsAny<Customer>()))
            .Returns(customerDto);

        // Act
        var result = await _service.CreateAsync(request);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Data.Should().Be(customerDto);

        _mockRepository.Verify(r => r.AddAsync(It.IsAny<Customer>(), It.IsAny<CancellationToken>()), Times.Once);
        _mockMapper.Verify(m => m.Map<CustomerDto>(It.IsAny<Customer>()), Times.Once);
    }
}
```

## Deliverables

1. **Test Base Classes**: Common test infrastructure
2. **Domain Tests**: Entity and value object tests
3. **Service Tests**: Business logic with mocking
4. **Repository Tests**: Data access layer tests
5. **Controller Tests**: API endpoint tests
6. **Handler Tests**: CQRS command/query tests
7. **Validator Tests**: Input validation tests
8. **Integration Tests**: End-to-end scenarios
9. **Test Utilities**: Builders and helpers
10. **Performance Tests**: Load and stress tests

## Validation Checklist

- [ ] All business logic covered by unit tests
- [ ] Mocking strategy isolates dependencies
- [ ] Test naming follows conventions
- [ ] AAA pattern consistently applied
- [ ] Edge cases and exceptions tested
- [ ] Test coverage meets requirements (80%+)
- [ ] Tests are fast and reliable
- [ ] No test dependencies or ordering
- [ ] Proper assertions with FluentAssertions
- [ ] Test categories properly defined