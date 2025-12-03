using CleanArchitectureDemo.Domain.Enums;

namespace CleanArchitectureDemo.Domain.Common;

public class CustomerSearchCriteria
{
    public string? Name { get; set; }
    public string? Email { get; set; }
    public CustomerType? Type { get; set; }
    public CustomerStatus? Status { get; set; }
    public int PageNumber { get; set; } = 1;
    public int PageSize { get; set; } = 10;
}