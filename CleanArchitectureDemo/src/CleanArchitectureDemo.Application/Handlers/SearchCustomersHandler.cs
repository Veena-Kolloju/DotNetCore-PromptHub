using AutoMapper;
using CleanArchitectureDemo.Application.Common;
using CleanArchitectureDemo.Application.DTOs;
using CleanArchitectureDemo.Application.Queries;
using CleanArchitectureDemo.Domain.Common;
using CleanArchitectureDemo.Domain.Interfaces;
using MediatR;

namespace CleanArchitectureDemo.Application.Handlers;

public class SearchCustomersHandler : IRequestHandler<SearchCustomersQuery, Result<PagedResult<CustomerDto>>>
{
    private readonly ICustomerRepository _customerRepository;
    private readonly IMapper _mapper;

    public SearchCustomersHandler(ICustomerRepository customerRepository, IMapper mapper)
    {
        _customerRepository = customerRepository;
        _mapper = mapper;
    }

    public async Task<Result<PagedResult<CustomerDto>>> Handle(SearchCustomersQuery request, CancellationToken cancellationToken)
    {
        var criteria = new CustomerSearchCriteria
        {
            Name = request.Name,
            Email = request.Email,
            Type = request.Type,
            Status = request.Status,
            PageNumber = request.PageNumber,
            PageSize = request.PageSize
        };

        var result = await _customerRepository.SearchAsync(criteria, cancellationToken);
        
        var dtoResult = new PagedResult<CustomerDto>
        {
            Items = _mapper.Map<IEnumerable<CustomerDto>>(result.Items),
            TotalCount = result.TotalCount,
            PageNumber = result.PageNumber,
            PageSize = result.PageSize
        };

        return Result<PagedResult<CustomerDto>>.Success(dtoResult);
    }
}