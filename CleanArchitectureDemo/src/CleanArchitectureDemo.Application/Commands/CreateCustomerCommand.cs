using CleanArchitectureDemo.Application.DTOs;
using MediatR;

namespace CleanArchitectureDemo.Application.Commands;

public record CreateCustomerCommand(
    string Name,
    string Email,
    string Phone) : IRequest<CustomerDto>;