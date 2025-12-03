using CleanArchitectureDemo.Application.DTOs;
using MediatR;

namespace CleanArchitectureDemo.Application.Queries;

public record GetCustomerByIdQuery(Guid Id) : IRequest<CustomerDto?>;