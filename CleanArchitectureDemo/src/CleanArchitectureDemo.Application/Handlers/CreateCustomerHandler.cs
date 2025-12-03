using AutoMapper;
using CleanArchitectureDemo.Application.Commands;
using CleanArchitectureDemo.Application.DTOs;
using CleanArchitectureDemo.Domain.Entities;
using CleanArchitectureDemo.Domain.Interfaces;
using CleanArchitectureDemo.Domain.ValueObjects;
using MediatR;

namespace CleanArchitectureDemo.Application.Handlers;

public class CreateCustomerHandler : IRequestHandler<CreateCustomerCommand, CustomerDto>
{
    private readonly IRepository<Customer> _repository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IMapper _mapper;

    public CreateCustomerHandler(IRepository<Customer> repository, IUnitOfWork unitOfWork, IMapper mapper)
    {
        _repository = repository;
        _unitOfWork = unitOfWork;
        _mapper = mapper;
    }

    public async Task<CustomerDto> Handle(CreateCustomerCommand request, CancellationToken cancellationToken)
    {
        var email = new Email(request.Email);
        var phone = new PhoneNumber(request.Phone);
        var customer = new Customer(request.Name, email, phone);
        
        await _repository.AddAsync(customer, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        
        return _mapper.Map<CustomerDto>(customer);
    }
}