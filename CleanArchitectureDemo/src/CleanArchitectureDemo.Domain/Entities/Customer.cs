using CleanArchitectureDemo.Domain.Enums;
using CleanArchitectureDemo.Domain.Exceptions;
using CleanArchitectureDemo.Domain.ValueObjects;

namespace CleanArchitectureDemo.Domain.Entities;

public class Customer : BaseEntity
{
    public string Name { get; private set; }
    public Email Email { get; private set; }
    public PhoneNumber Phone { get; private set; }
    public CustomerType Type { get; private set; }
    public CustomerStatus Status { get; private set; }

    private Customer() { } // EF Core constructor

    public Customer(string name, Email email, PhoneNumber phone)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException("Name cannot be empty", nameof(name));
        
        Name = name;
        Email = email ?? throw new ArgumentNullException(nameof(email));
        Phone = phone ?? throw new ArgumentNullException(nameof(phone));
        Type = CustomerType.Regular;
        Status = CustomerStatus.Active;
    }

    public void UpdateContactInfo(Email email, PhoneNumber phone)
    {
        Email = email ?? throw new ArgumentNullException(nameof(email));
        Phone = phone ?? throw new ArgumentNullException(nameof(phone));
        UpdateTimestamp();
    }

    public void PromoteToVip()
    {
        if (Type == CustomerType.Vip)
            throw new DomainException("Customer is already VIP");

        Type = CustomerType.Vip;
        UpdateTimestamp();
    }

    public void Suspend()
    {
        if (Status == CustomerStatus.Suspended)
            throw new DomainException("Customer is already suspended");

        Status = CustomerStatus.Suspended;
        UpdateTimestamp();
    }

    public void Activate()
    {
        if (Status == CustomerStatus.Active)
            throw new DomainException("Customer is already active");

        Status = CustomerStatus.Active;
        UpdateTimestamp();
    }

    public decimal CalculateDiscount(decimal amount)
    {
        return Type switch
        {
            CustomerType.Vip => amount * 0.15m,
            CustomerType.Premium => amount * 0.10m,
            _ => 0m
        };
    }
}