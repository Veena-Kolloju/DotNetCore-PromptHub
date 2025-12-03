using AutoMapper;
using CleanArchitectureDemo.Application.Commands;
using CleanArchitectureDemo.Application.DTOs;
using CleanArchitectureDemo.Domain.Interfaces;
using CleanArchitectureDemo.Domain.ValueObjects;
using MediatR;

namespace CleanArchitectureDemo.Application.Handlers;

public class UpdateCustomerHandler : IRequestHandler<UpdateCustomerCommand, CustomerDto>
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly IMapper _mapper;

    public UpdateCustomerHandler(IUnitOfWork unitOfWork, IMapper mapper)
    {
        _unitOfWork = unitOfWork;
        _mapper = mapper;
    }

    public async Task<CustomerDto> Handle(UpdateCustomerCommand request, CancellationToken cancellationToken)
    {
        var customer = await _unitOfWork.Customers.GetByIdAsync(request.Id, cancellationToken);
        if (customer == null)
            throw new ArgumentException("Customer not found");

        var email = new Email(request.Email);
        var phone = new PhoneNumber(request.Phone);
        
        customer.UpdateContactInfo(email, phone);
        
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        
        return _mapper.Map<CustomerDto>(customer);
    }
}