namespace CleanArchitectureDemo.Application.DTOs;

public record CreateCustomerRequest
{
    public string Name { get; init; } = string.Empty;
    public string Email { get; init; } = string.Empty;
    public string Phone { get; init; } = string.Empty;
}