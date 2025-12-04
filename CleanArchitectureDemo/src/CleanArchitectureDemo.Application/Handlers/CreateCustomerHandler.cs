using AutoMapper;
using CleanArchitectureDemo.Application.Commands;
using CleanArchitectureDemo.Application.Common;
using CleanArchitectureDemo.Application.DTOs;
using CleanArchitectureDemo.Domain.Entities;
using CleanArchitectureDemo.Domain.Interfaces;
using CleanArchitectureDemo.Domain.ValueObjects;
using MediatR;

namespace CleanArchitectureDemo.Application.Handlers;

public class CreateCustomerHandler : IRequestHandler<CreateCustomerCommand, Result<CustomerDto>>
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly IMapper _mapper;

    public CreateCustomerHandler(IUnitOfWork unitOfWork, IMapper mapper)
    {
        _unitOfWork = unitOfWork;
        _mapper = mapper;
    }

    public async Task<Result<CustomerDto>> Handle(CreateCustomerCommand request, CancellationToken cancellationToken)
    {
        var existingCustomer = await _unitOfWork.Customers.GetByEmailAsync(request.Email, cancellationToken);
        if (existingCustomer != null)
            return Result<CustomerDto>.Failure("Customer with this email already exists");

        var email = new Email(request.Email);
        var phone = new PhoneNumber(request.Phone);
        var customer = new Customer(request.Name, email, phone);
        
        await _unitOfWork.Customers.AddAsync(customer, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        
        var customerDto = _mapper.Map<CustomerDto>(customer);
        return Result<CustomerDto>.Success(customerDto);
    }
}