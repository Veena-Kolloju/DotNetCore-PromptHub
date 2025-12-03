using AutoMapper;
using CleanArchitectureDemo.Application.Commands;
using CleanArchitectureDemo.Application.DTOs;
using CleanArchitectureDemo.Application.Handlers;
using CleanArchitectureDemo.Application.Mappings;
using CleanArchitectureDemo.Domain.Entities;
using CleanArchitectureDemo.Domain.Interfaces;
using FluentAssertions;
using Moq;
using Xunit;

namespace CleanArchitectureDemo.UnitTests.Handlers;

public class CreateCustomerHandlerTests
{
    private readonly Mock<IRepository<Customer>> _repositoryMock;
    private readonly Mock<IUnitOfWork> _unitOfWorkMock;
    private readonly IMapper _mapper;
    private readonly CreateCustomerHandler _handler;

    public CreateCustomerHandlerTests()
    {
        _repositoryMock = new Mock<IRepository<Customer>>();
        _unitOfWorkMock = new Mock<IUnitOfWork>();
        
        var config = new MapperConfiguration(cfg => cfg.AddProfile<CustomerMappingProfile>());
        _mapper = config.CreateMapper();
        
        _handler = new CreateCustomerHandler(_repositoryMock.Object, _unitOfWorkMock.Object, _mapper);
    }

    [Fact]
    public async Task Handle_ValidCommand_ReturnsCustomerDto()
    {
        // Arrange
        var command = new CreateCustomerCommand("John Doe", "john@example.com", "+1234567890");

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.Should().NotBeNull();
        result.Name.Should().Be("John Doe");
        result.Email.Should().Be("john@example.com");
        result.Phone.Should().Be("+1234567890");
        
        _repositoryMock.Verify(r => r.AddAsync(It.IsAny<Customer>(), It.IsAny<CancellationToken>()), Times.Once);
        _unitOfWorkMock.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }
}