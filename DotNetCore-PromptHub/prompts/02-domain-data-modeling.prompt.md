---
context: ../design-system/architecture-patterns.md
context: ../instructions/general-coding.instructions.md
---

# Domain & Data Modeling

Create comprehensive domain models, DTOs, and mapping configurations following Domain-Driven Design principles.

## Requirements

### 1. Domain Entities
- Rich domain entities with business logic
- Value objects for complex types
- Domain enums with business meaning
- Aggregate roots with invariant enforcement

### 2. DTOs and View Models
- Request/Response DTOs for API contracts
- Validation attributes and rules
- Mapping profiles between domain and DTOs

### 3. Validation Patterns
- FluentValidation for complex business rules
- Custom validators for domain-specific logic
- Async validation for database checks

## Example Implementation

### Base Entity
```csharp
public abstract class BaseEntity
{
    public int Id { get; protected set; }
    public DateTime CreatedAt { get; protected set; }
    public DateTime? UpdatedAt { get; protected set; }
    public string CreatedBy { get; protected set; }
    public string UpdatedBy { get; protected set; }
    public bool IsDeleted { get; protected set; }
    
    protected BaseEntity()
    {
        CreatedAt = DateTime.UtcNow;
    }
    
    public void MarkAsDeleted()
    {
        IsDeleted = true;
        UpdatedAt = DateTime.UtcNow;
    }
    
    public void UpdateTimestamp(string updatedBy = null)
    {
        UpdatedAt = DateTime.UtcNow;
        UpdatedBy = updatedBy;
    }
}
```

### Domain Entity Example
```csharp
public class Customer : BaseEntity
{
    public string Name { get; private set; }
    public Email Email { get; private set; }
    public PhoneNumber Phone { get; private set; }
    public CustomerType Type { get; private set; }
    public CustomerStatus Status { get; private set; }
    public Address Address { get; private set; }
    
    private readonly List<Order> _orders = new();
    public IReadOnlyCollection<Order> Orders => _orders.AsReadOnly();
    
    private Customer() { } // EF Core constructor
    
    public Customer(string name, Email email, PhoneNumber phone)
    {
        Name = Guard.Against.NullOrWhiteSpace(name, nameof(name));
        Email = Guard.Against.Null(email, nameof(email));
        Phone = Guard.Against.Null(phone, nameof(phone));
        Type = CustomerType.Regular;
        Status = CustomerStatus.Active;
    }
    
    public void UpdateContactInfo(Email email, PhoneNumber phone)
    {
        Email = Guard.Against.Null(email, nameof(email));
        Phone = Guard.Against.Null(phone, nameof(phone));
        UpdateTimestamp();
    }
    
    public void PromoteToVip()
    {
        if (Type == CustomerType.Vip)
            throw new DomainException("Customer is already VIP");
            
        Type = CustomerType.Vip;
        UpdateTimestamp();
    }
    
    public decimal CalculateDiscount(decimal amount)
    {
        return Type switch
        {
            CustomerType.Vip => amount * 0.15m,
            CustomerType.Premium => amount * 0.10m,
            _ => 0m
        };
    }
}
```

### Value Objects
```csharp
public class Email : ValueObject
{
    public string Value { get; private set; }
    
    private Email() { }
    
    public Email(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
            throw new ArgumentException("Email cannot be empty", nameof(value));
            
        if (!IsValidEmail(value))
            throw new ArgumentException("Invalid email format", nameof(value));
            
        Value = value.ToLowerInvariant();
    }
    
    private static bool IsValidEmail(string email)
    {
        return Regex.IsMatch(email, @"^[^@\s]+@[^@\s]+\.[^@\s]+$");
    }
    
    protected override IEnumerable<object> GetEqualityComponents()
    {
        yield return Value;
    }
    
    public static implicit operator string(Email email) => email.Value;
    public static explicit operator Email(string email) => new(email);
}

public class Address : ValueObject
{
    public string Street { get; private set; }
    public string City { get; private set; }
    public string State { get; private set; }
    public string ZipCode { get; private set; }
    public string Country { get; private set; }
    
    private Address() { }
    
    public Address(string street, string city, string state, string zipCode, string country)
    {
        Street = Guard.Against.NullOrWhiteSpace(street, nameof(street));
        City = Guard.Against.NullOrWhiteSpace(city, nameof(city));
        State = Guard.Against.NullOrWhiteSpace(state, nameof(state));
        ZipCode = Guard.Against.NullOrWhiteSpace(zipCode, nameof(zipCode));
        Country = Guard.Against.NullOrWhiteSpace(country, nameof(country));
    }
    
    protected override IEnumerable<object> GetEqualityComponents()
    {
        yield return Street;
        yield return City;
        yield return State;
        yield return ZipCode;
        yield return Country;
    }
}
```

### Domain Enums
```csharp
public enum CustomerType
{
    Regular = 1,
    Premium = 2,
    Vip = 3
}

public enum CustomerStatus
{
    Active = 1,
    Inactive = 2,
    Suspended = 3,
    Deleted = 4
}

public enum OrderStatus
{
    Pending = 1,
    Processing = 2,
    Shipped = 3,
    Delivered = 4,
    Cancelled = 5,
    Returned = 6
}
```

### Request/Response DTOs
```csharp
// Request DTOs
public record CreateCustomerRequest
{
    public string Name { get; init; }
    public string Email { get; init; }
    public string Phone { get; init; }
    public AddressDto Address { get; init; }
}

public record UpdateCustomerRequest
{
    public string Name { get; init; }
    public string Email { get; init; }
    public string Phone { get; init; }
    public AddressDto Address { get; init; }
}

public record AddressDto
{
    public string Street { get; init; }
    public string City { get; init; }
    public string State { get; init; }
    public string ZipCode { get; init; }
    public string Country { get; init; }
}

// Response DTOs
public record CustomerDto
{
    public int Id { get; init; }
    public string Name { get; init; }
    public string Email { get; init; }
    public string Phone { get; init; }
    public string Type { get; init; }
    public string Status { get; init; }
    public AddressDto Address { get; init; }
    public DateTime CreatedAt { get; init; }
    public DateTime? UpdatedAt { get; init; }
}

public record CustomerSummaryDto
{
    public int Id { get; init; }
    public string Name { get; init; }
    public string Email { get; init; }
    public string Type { get; init; }
    public string Status { get; init; }
    public int OrderCount { get; init; }
    public decimal TotalSpent { get; init; }
}
```

### AutoMapper Profiles
```csharp
public class CustomerMappingProfile : Profile
{
    public CustomerMappingProfile()
    {
        CreateMap<Customer, CustomerDto>()
            .ForMember(dest => dest.Email, opt => opt.MapFrom(src => src.Email.Value))
            .ForMember(dest => dest.Phone, opt => opt.MapFrom(src => src.Phone.Value))
            .ForMember(dest => dest.Type, opt => opt.MapFrom(src => src.Type.ToString()))
            .ForMember(dest => dest.Status, opt => opt.MapFrom(src => src.Status.ToString()));
            
        CreateMap<Customer, CustomerSummaryDto>()
            .ForMember(dest => dest.Email, opt => opt.MapFrom(src => src.Email.Value))
            .ForMember(dest => dest.OrderCount, opt => opt.MapFrom(src => src.Orders.Count))
            .ForMember(dest => dest.TotalSpent, opt => opt.MapFrom(src => src.Orders.Sum(o => o.TotalAmount)));
            
        CreateMap<Address, AddressDto>().ReverseMap();
        
        CreateMap<CreateCustomerRequest, Customer>()
            .ConstructUsing(src => new Customer(
                src.Name,
                new Email(src.Email),
                new PhoneNumber(src.Phone)))
            .ForMember(dest => dest.Address, opt => opt.MapFrom(src => 
                new Address(src.Address.Street, src.Address.City, src.Address.State, 
                           src.Address.ZipCode, src.Address.Country)));
    }
}
```

### FluentValidation Validators
```csharp
public class CreateCustomerRequestValidator : AbstractValidator<CreateCustomerRequest>
{
    private readonly ICustomerRepository _customerRepository;
    
    public CreateCustomerRequestValidator(ICustomerRepository customerRepository)
    {
        _customerRepository = customerRepository;
        
        RuleFor(x => x.Name)
            .NotEmpty().WithMessage("Name is required")
            .MaximumLength(100).WithMessage("Name cannot exceed 100 characters")
            .Matches(@"^[a-zA-Z\s]+$").WithMessage("Name can only contain letters and spaces");
            
        RuleFor(x => x.Email)
            .NotEmpty().WithMessage("Email is required")
            .EmailAddress().WithMessage("Invalid email format")
            .MustAsync(BeUniqueEmail).WithMessage("Email already exists");
            
        RuleFor(x => x.Phone)
            .NotEmpty().WithMessage("Phone is required")
            .Matches(@"^\+?[1-9]\d{1,14}$").WithMessage("Invalid phone format");
            
        RuleFor(x => x.Address)
            .NotNull().WithMessage("Address is required")
            .SetValidator(new AddressValidator());
    }
    
    private async Task<bool> BeUniqueEmail(string email, CancellationToken cancellationToken)
    {
        return !await _customerRepository.ExistsByEmailAsync(email, cancellationToken);
    }
}

public class AddressValidator : AbstractValidator<AddressDto>
{
    public AddressValidator()
    {
        RuleFor(x => x.Street)
            .NotEmpty().WithMessage("Street is required")
            .MaximumLength(200).WithMessage("Street cannot exceed 200 characters");
            
        RuleFor(x => x.City)
            .NotEmpty().WithMessage("City is required")
            .MaximumLength(100).WithMessage("City cannot exceed 100 characters");
            
        RuleFor(x => x.State)
            .NotEmpty().WithMessage("State is required")
            .MaximumLength(50).WithMessage("State cannot exceed 50 characters");
            
        RuleFor(x => x.ZipCode)
            .NotEmpty().WithMessage("Zip code is required")
            .Matches(@"^\d{5}(-\d{4})?$").WithMessage("Invalid zip code format");
            
        RuleFor(x => x.Country)
            .NotEmpty().WithMessage("Country is required")
            .MaximumLength(100).WithMessage("Country cannot exceed 100 characters");
    }
}
```

### Custom Validation Attributes
```csharp
public class ValidEmailAttribute : ValidationAttribute
{
    protected override ValidationResult IsValid(object value, ValidationContext validationContext)
    {
        if (value is string email && !string.IsNullOrWhiteSpace(email))
        {
            try
            {
                var emailObj = new Email(email);
                return ValidationResult.Success;
            }
            catch (ArgumentException ex)
            {
                return new ValidationResult(ex.Message);
            }
        }
        
        return new ValidationResult("Invalid email format");
    }
}

public class UniqueEmailAttribute : ValidationAttribute
{
    protected override ValidationResult IsValid(object value, ValidationContext validationContext)
    {
        if (value is string email)
        {
            var repository = validationContext.GetService<ICustomerRepository>();
            var exists = repository.ExistsByEmailAsync(email, CancellationToken.None).Result;
            
            if (exists)
                return new ValidationResult("Email already exists");
        }
        
        return ValidationResult.Success;
    }
}
```

## Deliverables

Generate the following components:

1. **Base Entity**: Abstract base class with audit fields
2. **Domain Entities**: Rich entities with business logic
3. **Value Objects**: Immutable value objects with validation
4. **Domain Enums**: Business-meaningful enumerations
5. **Request DTOs**: Input models with validation attributes
6. **Response DTOs**: Output models for API responses
7. **AutoMapper Profiles**: Mapping configurations between domain and DTOs
8. **FluentValidation Validators**: Complex validation rules
9. **Custom Validators**: Domain-specific validation logic
10. **Guard Clauses**: Input validation helpers

## Validation Checklist

- [ ] Domain entities encapsulate business logic
- [ ] Value objects are immutable and validated
- [ ] DTOs are properly structured for API contracts
- [ ] Mapping profiles handle all conversions
- [ ] Validation rules cover all business requirements
- [ ] Custom validators implement domain logic
- [ ] Guard clauses protect invariants
- [ ] Enums have meaningful business names
- [ ] All models follow naming conventions
- [ ] Async validation implemented where needed