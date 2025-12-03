using AutoMapper;
using CleanArchitectureDemo.Application.DTOs;
using CleanArchitectureDemo.Application.Queries;
using CleanArchitectureDemo.Domain.Entities;
using CleanArchitectureDemo.Domain.Interfaces;
using MediatR;

namespace CleanArchitectureDemo.Application.Handlers;

public class GetCustomerByIdHandler : IRequestHandler<GetCustomerByIdQuery, CustomerDto?>
{
    private readonly IRepository<Customer> _repository;
    private readonly IMapper _mapper;

    public GetCustomerByIdHandler(IRepository<Customer> repository, IMapper mapper)
    {
        _repository = repository;
        _mapper = mapper;
    }

    public async Task<CustomerDto?> Handle(GetCustomerByIdQuery request, CancellationToken cancellationToken)
    {
        var customer = await _repository.GetByIdAsync(request.Id, cancellationToken);
        return customer == null ? null : _mapper.Map<CustomerDto>(customer);
    }
}