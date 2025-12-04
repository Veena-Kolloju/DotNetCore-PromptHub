using CleanArchitectureDemo.Application.Common;
using CleanArchitectureDemo.Application.DTOs;
using MediatR;

namespace CleanArchitectureDemo.Application.Commands;

public record UpdateCustomerCommand(
    Guid Id,
    string Name,
    string Email,
    string Phone) : IRequest<Result<CustomerDto>>;