namespace CleanArchitectureDemo.Application.DTOs;

public record CustomerDto(
    Guid Id,
    string Name,
    string Email,
    string Phone,
    string Type,
    string Status,
    DateTime CreatedAt,
    DateTime? UpdatedAt);