using AutoMapper;
using CleanArchitectureDemo.Application.DTOs;
using CleanArchitectureDemo.Application.Queries;
using CleanArchitectureDemo.Domain.Interfaces;
using MediatR;

namespace CleanArchitectureDemo.Application.Handlers;

public class GetCustomerByIdHandler : IRequestHandler<GetCustomerByIdQuery, CustomerDto?>
{
    private readonly ICustomerRepository _customerRepository;
    private readonly IMapper _mapper;

    public GetCustomerByIdHandler(ICustomerRepository customerRepository, IMapper mapper)
    {
        _customerRepository = customerRepository;
        _mapper = mapper;
    }

    public async Task<CustomerDto?> Handle(GetCustomerByIdQuery request, CancellationToken cancellationToken)
    {
        var customer = await _customerRepository.GetByIdAsync(request.Id, cancellationToken);
        return customer == null ? null : _mapper.Map<CustomerDto>(customer);
    }
}