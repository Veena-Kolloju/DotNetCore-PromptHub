using NetArchTest.Rules;
using Xunit;

namespace CleanArchitectureDemo.ArchitectureTests;

public class ArchitectureTests
{
    private const string DomainNamespace = "CleanArchitectureDemo.Domain";
    private const string ApplicationNamespace = "CleanArchitectureDemo.Application";
    private const string InfrastructureNamespace = "CleanArchitectureDemo.Infrastructure";
    private const string ApiNamespace = "CleanArchitectureDemo.API";

    [Fact]
    public void Domain_Should_Not_HaveDependencyOnOtherProjects()
    {
        var result = Types.InAssembly(typeof(Domain.Entities.BaseEntity).Assembly)
            .ShouldNot()
            .HaveDependencyOnAny(ApplicationNamespace, InfrastructureNamespace, ApiNamespace)
            .GetResult();

        Assert.True(result.IsSuccessful);
    }

    [Fact]
    public void Application_Should_Not_HaveDependencyOnInfrastructureOrApi()
    {
        var result = Types.InAssembly(typeof(Application.ApplicationServiceExtensions).Assembly)
            .ShouldNot()
            .HaveDependencyOnAny(InfrastructureNamespace, ApiNamespace)
            .GetResult();

        Assert.True(result.IsSuccessful);
    }

    [Fact]
    public void Infrastructure_Should_Not_HaveDependencyOnApi()
    {
        var result = Types.InAssembly(typeof(Infrastructure.InfrastructureServiceExtensions).Assembly)
            .ShouldNot()
            .HaveDependencyOn(ApiNamespace)
            .GetResult();

        Assert.True(result.IsSuccessful);
    }
}