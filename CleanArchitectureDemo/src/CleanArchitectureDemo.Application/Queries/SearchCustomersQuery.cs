using CleanArchitectureDemo.Application.Common;
using CleanArchitectureDemo.Application.DTOs;
using CleanArchitectureDemo.Domain.Common;
using CleanArchitectureDemo.Domain.Enums;
using MediatR;

namespace CleanArchitectureDemo.Application.Queries;

public record SearchCustomersQuery : IRequest<Result<PagedResult<CustomerDto>>>
{
    public string? Name { get; init; }
    public string? Email { get; init; }
    public CustomerType? Type { get; init; }
    public CustomerStatus? Status { get; init; }
    public int PageNumber { get; init; } = 1;
    public int PageSize { get; init; } = 10;
}