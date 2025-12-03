using CleanArchitectureDemo.Domain.Common;
using CleanArchitectureDemo.Domain.Entities;

namespace CleanArchitectureDemo.Domain.Interfaces;

public interface ICustomerRepository : IRepository<Customer>
{
    Task<Customer?> GetByEmailAsync(string email, CancellationToken cancellationToken = default);
    Task<bool> ExistsByEmailAsync(string email, CancellationToken cancellationToken = default);
    Task<IEnumerable<Customer>> GetVipCustomersAsync(CancellationToken cancellationToken = default);
    Task<PagedResult<Customer>> SearchAsync(CustomerSearchCriteria criteria, CancellationToken cancellationToken = default);
}