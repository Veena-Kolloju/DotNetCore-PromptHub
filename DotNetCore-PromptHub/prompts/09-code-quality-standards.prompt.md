---
context: ../design-system/architecture-patterns.md
context: ../instructions/general-coding.instructions.md
context: ../instructions/guardrails.md
---

# Code Quality & Standards

Implement comprehensive code quality standards, clean code guidelines, and automated quality checks for .NET Core applications.

## Requirements

### 1. Clean Code Guidelines
- SOLID principles implementation
- DRY (Don't Repeat Yourself) principle
- Meaningful naming conventions
- Method and class size limitations
- Error handling best practices

### 2. Code Analysis Tools
- Static code analysis with SonarQube
- EditorConfig for consistent formatting
- StyleCop for style enforcement
- FxCop analyzers for code quality
- Custom analyzers for domain rules

### 3. Refactoring Patterns
- Extract method and class refactoring
- Replace conditional with polymorphism
- Introduce parameter object
- Replace magic numbers with constants
- Eliminate code duplication

## Example Implementation

### EditorConfig (.editorconfig)
```ini
root = true

[*]
charset = utf-8
end_of_line = crlf
insert_final_newline = true
trim_trailing_whitespace = true

[*.{cs,csx,vb,vbx}]
indent_style = space
indent_size = 4

[*.{json,js,ts,html,css,scss}]
indent_style = space
indent_size = 2

[*.cs]
# Organize usings
dotnet_sort_system_directives_first = true
dotnet_separate_import_directive_groups = false

# Code style rules
csharp_new_line_before_open_brace = all
csharp_new_line_before_else = true
csharp_new_line_before_catch = true
csharp_new_line_before_finally = true
csharp_new_line_before_members_in_object_initializers = true
csharp_new_line_before_members_in_anonymous_types = true

# Indentation preferences
csharp_indent_case_contents = true
csharp_indent_switch_labels = true
csharp_indent_labels = flush_left

# Space preferences
csharp_space_after_cast = false
csharp_space_after_keywords_in_control_flow_statements = true
csharp_space_between_method_call_parameter_list_parentheses = false
csharp_space_between_method_declaration_parameter_list_parentheses = false

# Wrapping preferences
csharp_preserve_single_line_statements = true
csharp_preserve_single_line_blocks = true

# Naming conventions
dotnet_naming_rule.interfaces_should_be_prefixed_with_i.severity = warning
dotnet_naming_rule.interfaces_should_be_prefixed_with_i.symbols = interface
dotnet_naming_rule.interfaces_should_be_prefixed_with_i.style = prefix_interface_with_i

dotnet_naming_rule.types_should_be_pascal_case.severity = warning
dotnet_naming_rule.types_should_be_pascal_case.symbols = types
dotnet_naming_rule.types_should_be_pascal_case.style = pascal_case

dotnet_naming_rule.non_field_members_should_be_pascal_case.severity = warning
dotnet_naming_rule.non_field_members_should_be_pascal_case.symbols = non_field_members
dotnet_naming_rule.non_field_members_should_be_pascal_case.style = pascal_case

# Symbol specifications
dotnet_naming_symbols.interface.applicable_kinds = interface
dotnet_naming_symbols.interface.applicable_accessibilities = public, internal, private, protected, protected_internal, private_protected

dotnet_naming_symbols.types.applicable_kinds = class, struct, interface, enum
dotnet_naming_symbols.types.applicable_accessibilities = public, internal, private, protected, protected_internal, private_protected

dotnet_naming_symbols.non_field_members.applicable_kinds = property, event, method
dotnet_naming_symbols.non_field_members.applicable_accessibilities = public, internal, private, protected, protected_internal, private_protected

# Naming styles
dotnet_naming_style.pascal_case.capitalization = pascal_case
dotnet_naming_style.prefix_interface_with_i.capitalization = pascal_case
dotnet_naming_style.prefix_interface_with_i.required_prefix = I
```

### Code Quality Analyzers (Directory.Build.props)
```xml
<Project>
  <PropertyGroup>
    <TargetFramework>net8.0</TargetFramework>
    <Nullable>enable</Nullable>
    <TreatWarningsAsErrors>true</TreatWarningsAsErrors>
    <WarningsAsErrors />
    <WarningsNotAsErrors>CS1591</WarningsNotAsErrors>
    <GenerateDocumentationFile>true</GenerateDocumentationFile>
    <EnforceCodeStyleInBuild>true</EnforceCodeStyleInBuild>
    <EnableNETAnalyzers>true</EnableNETAnalyzers>
    <AnalysisLevel>latest</AnalysisLevel>
  </PropertyGroup>

  <ItemGroup>
    <PackageReference Include="StyleCop.Analyzers" Version="1.2.0-beta.435">
      <PrivateAssets>all</PrivateAssets>
      <IncludeAssets>runtime; build; native; contentfiles; analyzers</IncludeAssets>
    </PackageReference>
    <PackageReference Include="Microsoft.CodeAnalysis.NetAnalyzers" Version="7.0.4">
      <PrivateAssets>all</PrivateAssets>
      <IncludeAssets>runtime; build; native; contentfiles; analyzers</IncludeAssets>
    </PackageReference>
    <PackageReference Include="SonarAnalyzer.CSharp" Version="9.12.0.78982">
      <PrivateAssets>all</PrivateAssets>
      <IncludeAssets>runtime; build; native; contentfiles; analyzers</IncludeAssets>
    </PackageReference>
  </ItemGroup>

  <ItemGroup>
    <AdditionalFiles Include="stylecop.json" />
  </ItemGroup>
</Project>
```

### StyleCop Configuration (stylecop.json)
```json
{
  "$schema": "https://raw.githubusercontent.com/DotNetAnalyzers/StyleCopAnalyzers/master/StyleCop.Analyzers/StyleCop.Analyzers/Settings/stylecop.schema.json",
  "settings": {
    "documentationRules": {
      "companyName": "YourCompany",
      "copyrightText": "Copyright (c) {companyName}. All rights reserved.",
      "xmlHeader": true,
      "fileNamingConvention": "stylecop"
    },
    "orderingRules": {
      "usingDirectivesPlacement": "outsideNamespace",
      "elementOrder": [
        "kind",
        "accessibility",
        "constant",
        "static",
        "readonly"
      ]
    },
    "namingRules": {
      "allowCommonHungarianPrefixes": false,
      "allowedHungarianPrefixes": []
    },
    "maintainabilityRules": {
      "topLevelTypes": [
        "class",
        "interface",
        "struct",
        "delegate",
        "enum"
      ]
    },
    "layoutRules": {
      "newlineAtEndOfFile": "require",
      "allowConsecutiveUsings": true
    }
  }
}
```

### Clean Code Examples

#### Before: Poorly Written Code
```csharp
public class CustomerService
{
    private readonly IRepository<Customer> repo;
    private readonly IMapper map;
    private readonly ILogger log;

    public CustomerService(IRepository<Customer> r, IMapper m, ILogger l)
    {
        repo = r;
        map = m;
        log = l;
    }

    public async Task<CustomerDto> CreateCustomer(string n, string e, string p, string s, string c, string st, string z, string co)
    {
        if (string.IsNullOrEmpty(n) || string.IsNullOrEmpty(e))
            throw new Exception("Invalid data");

        var existing = await repo.FindAsync(x => x.Email.Value == e);
        if (existing.Any())
            throw new Exception("Customer exists");

        var customer = new Customer(n, new Email(e), new PhoneNumber(p));
        if (!string.IsNullOrEmpty(s))
        {
            customer.UpdateAddress(new Address(s, c, st, z, co));
        }

        await repo.AddAsync(customer);
        log.LogInformation("Customer created");
        return map.Map<CustomerDto>(customer);
    }
}
```

#### After: Clean Code Implementation
```csharp
/// <summary>
/// Service for managing customer operations including creation, updates, and business logic.
/// </summary>
public class CustomerService : ICustomerService
{
    private readonly ICustomerRepository _customerRepository;
    private readonly IMapper _mapper;
    private readonly ILogger<CustomerService> _logger;
    private readonly IEventBus _eventBus;

    public CustomerService(
        ICustomerRepository customerRepository,
        IMapper mapper,
        ILogger<CustomerService> logger,
        IEventBus eventBus)
    {
        _customerRepository = Guard.Against.Null(customerRepository, nameof(customerRepository));
        _mapper = Guard.Against.Null(mapper, nameof(mapper));
        _logger = Guard.Against.Null(logger, nameof(logger));
        _eventBus = Guard.Against.Null(eventBus, nameof(eventBus));
    }

    /// <summary>
    /// Creates a new customer with the provided information.
    /// </summary>
    /// <param name="request">Customer creation request containing all required information.</param>
    /// <param name="cancellationToken">Cancellation token for async operation.</param>
    /// <returns>Result containing the created customer DTO or error information.</returns>
    public async Task<Result<CustomerDto>> CreateCustomerAsync(
        CreateCustomerRequest request, 
        CancellationToken cancellationToken = default)
    {
        Guard.Against.Null(request, nameof(request));

        try
        {
            await ValidateCustomerDoesNotExistAsync(request.Email, cancellationToken);

            var customer = await CreateCustomerEntityAsync(request);
            await SaveCustomerAsync(customer, cancellationToken);
            await PublishCustomerCreatedEventAsync(customer, cancellationToken);

            var customerDto = _mapper.Map<CustomerDto>(customer);
            
            _logger.LogInformation(
                "Customer created successfully with ID {CustomerId} and email {Email}", 
                customer.Id, 
                customer.Email.Value);

            return Result<CustomerDto>.Success(customerDto);
        }
        catch (DomainException ex)
        {
            _logger.LogWarning(ex, "Domain validation failed while creating customer with email {Email}", request.Email);
            return Result<CustomerDto>.Failure(ex.Message);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error occurred while creating customer with email {Email}", request.Email);
            return Result<CustomerDto>.Failure("An unexpected error occurred while creating the customer");
        }
    }

    private async Task ValidateCustomerDoesNotExistAsync(string email, CancellationToken cancellationToken)
    {
        var existingCustomer = await _customerRepository.GetByEmailAsync(email, cancellationToken);
        if (existingCustomer != null)
        {
            throw new DomainException($"Customer with email '{email}' already exists");
        }
    }

    private async Task<Customer> CreateCustomerEntityAsync(CreateCustomerRequest request)
    {
        var customer = new Customer(
            request.Name,
            new Email(request.Email),
            new PhoneNumber(request.Phone));

        if (request.Address != null)
        {
            var address = new Address(
                request.Address.Street,
                request.Address.City,
                request.Address.State,
                request.Address.ZipCode,
                request.Address.Country);

            customer.UpdateAddress(address);
        }

        return customer;
    }

    private async Task SaveCustomerAsync(Customer customer, CancellationToken cancellationToken)
    {
        await _customerRepository.AddAsync(customer, cancellationToken);
    }

    private async Task PublishCustomerCreatedEventAsync(Customer customer, CancellationToken cancellationToken)
    {
        var customerCreatedEvent = new CustomerCreatedEvent
        {
            CustomerId = customer.Id,
            CustomerName = customer.Name,
            Email = customer.Email.Value,
            Type = customer.Type
        };

        await _eventBus.PublishAsync(customerCreatedEvent, cancellationToken);
    }
}
```

### Code Quality Metrics and Rules
```csharp
/// <summary>
/// Code quality metrics and thresholds for the application.
/// </summary>
public static class CodeQualityMetrics
{
    // Method complexity thresholds
    public const int MaxCyclomaticComplexity = 10;
    public const int MaxMethodLength = 20;
    public const int MaxParameterCount = 5;
    
    // Class design thresholds
    public const int MaxClassLength = 300;
    public const int MaxConstructorParameters = 5;
    public const int MaxPublicMethods = 20;
    
    // Naming conventions
    public const int MinMethodNameLength = 3;
    public const int MaxMethodNameLength = 50;
    public const int MinVariableNameLength = 2;
    
    // Test coverage requirements
    public const double MinCodeCoverage = 80.0;
    public const double MinBranchCoverage = 70.0;
    
    // Performance thresholds
    public const int MaxApiResponseTimeMs = 500;
    public const int MaxDatabaseQueryTimeMs = 100;
}

/// <summary>
/// Custom analyzer rules for domain-specific quality checks.
/// </summary>
public static class CustomAnalyzerRules
{
    // Domain entity rules
    public const string EntityMustHavePrivateConstructor = "DOMAIN001";
    public const string EntityMustInheritFromBaseEntity = "DOMAIN002";
    public const string ValueObjectMustBeImmutable = "DOMAIN003";
    
    // Repository rules
    public const string RepositoryMustImplementInterface = "REPO001";
    public const string RepositoryMustUseAsyncMethods = "REPO002";
    
    // Service rules
    public const string ServiceMustHaveInterface = "SERVICE001";
    public const string ServiceMustUseResultPattern = "SERVICE002";
    public const string ServiceMustLogOperations = "SERVICE003";
    
    // API rules
    public const string ControllerMustHaveAuthorization = "API001";
    public const string ControllerMustUseStandardResponses = "API002";
    public const string ControllerMustValidateInput = "API003";
}
```

### Refactoring Guidelines
```csharp
/// <summary>
/// Guidelines and examples for common refactoring patterns.
/// </summary>
public static class RefactoringGuidelines
{
    /// <summary>
    /// Extract Method: Break down large methods into smaller, focused methods.
    /// </summary>
    public class ExtractMethodExample
    {
        // Before: Large method with multiple responsibilities
        public async Task<CustomerDto> ProcessCustomerOrderBefore(CreateOrderRequest request)
        {
            // Validate customer
            var customer = await _customerRepository.GetByIdAsync(request.CustomerId);
            if (customer == null)
                throw new NotFoundException("Customer not found");

            // Calculate totals
            decimal subtotal = 0;
            foreach (var item in request.Items)
            {
                var product = await _productRepository.GetByIdAsync(item.ProductId);
                subtotal += product.Price * item.Quantity;
            }
            var tax = subtotal * 0.08m;
            var total = subtotal + tax;

            // Create order
            var order = new Order(customer.Id, DateTime.UtcNow);
            foreach (var item in request.Items)
            {
                order.AddItem(item.ProductId, item.Quantity, item.UnitPrice);
            }

            // Process payment
            var paymentRequest = new PaymentRequest
            {
                Amount = total,
                CustomerId = customer.Id,
                PaymentMethod = request.PaymentMethod
            };
            var paymentResult = await _paymentService.ProcessPaymentAsync(paymentRequest);

            return _mapper.Map<CustomerDto>(customer);
        }

        // After: Extracted into focused methods
        public async Task<CustomerDto> ProcessCustomerOrderAfter(CreateOrderRequest request)
        {
            var customer = await ValidateCustomerAsync(request.CustomerId);
            var orderTotal = await CalculateOrderTotalAsync(request.Items);
            var order = await CreateOrderAsync(customer.Id, request.Items);
            await ProcessPaymentAsync(customer.Id, orderTotal, request.PaymentMethod);

            return _mapper.Map<CustomerDto>(customer);
        }

        private async Task<Customer> ValidateCustomerAsync(int customerId)
        {
            var customer = await _customerRepository.GetByIdAsync(customerId);
            if (customer == null)
                throw new NotFoundException("Customer not found");
            return customer;
        }

        private async Task<decimal> CalculateOrderTotalAsync(List<OrderItemRequest> items)
        {
            decimal subtotal = 0;
            foreach (var item in items)
            {
                var product = await _productRepository.GetByIdAsync(item.ProductId);
                subtotal += product.Price * item.Quantity;
            }
            
            var tax = subtotal * TaxRate;
            return subtotal + tax;
        }
    }

    /// <summary>
    /// Replace Magic Numbers: Use named constants instead of magic numbers.
    /// </summary>
    public class ReplaceMagicNumbersExample
    {
        // Before: Magic numbers scattered throughout code
        public decimal CalculateDiscountBefore(CustomerType type, decimal amount)
        {
            return type switch
            {
                CustomerType.Regular => amount * 0.05m,
                CustomerType.Premium => amount * 0.10m,
                CustomerType.Vip => amount * 0.15m,
                _ => 0
            };
        }

        // After: Named constants
        private static class DiscountRates
        {
            public const decimal Regular = 0.05m;
            public const decimal Premium = 0.10m;
            public const decimal Vip = 0.15m;
        }

        public decimal CalculateDiscountAfter(CustomerType type, decimal amount)
        {
            return type switch
            {
                CustomerType.Regular => amount * DiscountRates.Regular,
                CustomerType.Premium => amount * DiscountRates.Premium,
                CustomerType.Vip => amount * DiscountRates.Vip,
                _ => 0
            };
        }
    }

    /// <summary>
    /// Introduce Parameter Object: Group related parameters into a single object.
    /// </summary>
    public class IntroduceParameterObjectExample
    {
        // Before: Too many parameters
        public async Task<Customer> CreateCustomerBefore(
            string name, 
            string email, 
            string phone, 
            string street, 
            string city, 
            string state, 
            string zipCode, 
            string country)
        {
            // Implementation
            return null;
        }

        // After: Parameter object
        public async Task<Customer> CreateCustomerAfter(CustomerCreationData customerData)
        {
            // Implementation using customerData properties
            return null;
        }
    }
}

public class CustomerCreationData
{
    public string Name { get; set; }
    public string Email { get; set; }
    public string Phone { get; set; }
    public AddressData Address { get; set; }
}

public class AddressData
{
    public string Street { get; set; }
    public string City { get; set; }
    public string State { get; set; }
    public string ZipCode { get; set; }
    public string Country { get; set; }
}
```

### Quality Gates and CI/CD Integration
```yaml
# .github/workflows/code-quality.yml
name: Code Quality Check

on:
  pull_request:
    branches: [ main, develop ]
  push:
    branches: [ main, develop ]

jobs:
  code-quality:
    runs-on: ubuntu-latest
    
    steps:
    - uses: actions/checkout@v4
      with:
        fetch-depth: 0
    
    - name: Setup .NET
      uses: actions/setup-dotnet@v3
      with:
        dotnet-version: '8.0.x'
    
    - name: Restore dependencies
      run: dotnet restore
    
    - name: Build
      run: dotnet build --no-restore --configuration Release
    
    - name: Run tests with coverage
      run: dotnet test --no-build --configuration Release --collect:"XPlat Code Coverage" --results-directory ./coverage
    
    - name: Code Coverage Report
      uses: irongut/CodeCoverageSummary@v1.3.0
      with:
        filename: coverage/**/coverage.cobertura.xml
        badge: true
        fail_below_min: true
        format: markdown
        hide_branch_rate: false
        hide_complexity: true
        indicators: true
        output: both
        thresholds: '60 80'
    
    - name: SonarCloud Scan
      uses: SonarSource/sonarcloud-github-action@master
      env:
        GITHUB_TOKEN: ${{ secrets.GITHUB_TOKEN }}
        SONAR_TOKEN: ${{ secrets.SONAR_TOKEN }}
    
    - name: Quality Gate Check
      run: |
        # Check if quality gate passed
        if [ "${{ steps.sonarcloud.outputs.quality-gate-status }}" != "PASSED" ]; then
          echo "Quality gate failed"
          exit 1
        fi
```

## Deliverables

1. **EditorConfig**: Consistent code formatting rules
2. **StyleCop Configuration**: Code style enforcement
3. **Analyzer Rules**: Static code analysis setup
4. **Clean Code Examples**: Before/after code improvements
5. **Refactoring Guidelines**: Common refactoring patterns
6. **Quality Metrics**: Code quality thresholds and rules
7. **CI/CD Integration**: Automated quality checks
8. **Code Review Checklist**: Manual review guidelines
9. **Custom Analyzers**: Domain-specific quality rules
10. **Quality Gates**: Automated quality enforcement

## Validation Checklist

- [ ] EditorConfig enforces consistent formatting
- [ ] StyleCop rules configured and enforced
- [ ] Static analysis tools integrated
- [ ] Code coverage meets minimum thresholds
- [ ] Cyclomatic complexity within limits
- [ ] Method and class sizes appropriate
- [ ] Naming conventions followed consistently
- [ ] SOLID principles applied throughout
- [ ] Error handling comprehensive and consistent
- [ ] Quality gates prevent poor code from merging