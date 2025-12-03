using AutoMapper;
using CleanArchitectureDemo.Application.DTOs;
using CleanArchitectureDemo.Domain.Entities;
using CleanArchitectureDemo.Domain.ValueObjects;

namespace CleanArchitectureDemo.Application.Mappings;

public class CustomerMappingProfile : Profile
{
    public CustomerMappingProfile()
    {
        CreateMap<Customer, CustomerDto>()
            .ForMember(dest => dest.Email, opt => opt.MapFrom(src => src.Email.Value))
            .ForMember(dest => dest.Phone, opt => opt.MapFrom(src => src.Phone.Value))
            .ForMember(dest => dest.Type, opt => opt.MapFrom(src => src.Type.ToString()))
            .ForMember(dest => dest.Status, opt => opt.MapFrom(src => src.Status.ToString()));
            
        CreateMap<CreateCustomerRequest, Customer>()
            .ConstructUsing(src => new Customer(
                src.Name,
                new Email(src.Email),
                new PhoneNumber(src.Phone)));
    }
}