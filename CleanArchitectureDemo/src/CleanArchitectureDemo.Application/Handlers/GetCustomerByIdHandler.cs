using AutoMapper;
using CleanArchitectureDemo.Application.Common;
using CleanArchitectureDemo.Application.DTOs;
using CleanArchitectureDemo.Application.Queries;
using CleanArchitectureDemo.Domain.Interfaces;
using MediatR;

namespace CleanArchitectureDemo.Application.Handlers;

public class GetCustomerByIdHandler : IRequestHandler<GetCustomerByIdQuery, Result<CustomerDto>>
{
    private readonly ICustomerRepository _customerRepository;
    private readonly IMapper _mapper;

    public GetCustomerByIdHandler(ICustomerRepository customerRepository, IMapper mapper)
    {
        _customerRepository = customerRepository;
        _mapper = mapper;
    }

    public async Task<Result<CustomerDto>> Handle(GetCustomerByIdQuery request, CancellationToken cancellationToken)
    {
        var customer = await _customerRepository.GetByIdAsync(request.Id, cancellationToken);
        
        if (customer == null)
            return Result<CustomerDto>.Failure("Customer not found");
            
        var customerDto = _mapper.Map<CustomerDto>(customer);
        return Result<CustomerDto>.Success(customerDto);
    }
}