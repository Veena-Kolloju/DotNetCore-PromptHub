using CleanArchitectureDemo.Application.Common;
using CleanArchitectureDemo.Application.DTOs;
using MediatR;

namespace CleanArchitectureDemo.Application.Commands;

public record CreateCustomerCommand(
    string Name,
    string Email,
    string Phone) : IRequest<Result<CustomerDto>>;