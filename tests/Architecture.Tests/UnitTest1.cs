using NetArchTest.Rules;
using Application.Devices.Commands;
using Domain;
using Infrastructure.EventSourcing;

namespace Architecture.Tests;

public class UnitTest1
{
    [Fact]
    public void Domain_Should_Not_Depend_On_Other_Layers()
    {
        var result = Types
            .InAssembly(typeof(Device).Assembly)
            .ShouldNot()
            .HaveDependencyOnAny(
                "Application",
                "Infrastructure",
                "API")
            .GetResult();

        Assert.True(result.IsSuccessful, "Domain layer must not depend on outer layers.");
    }

    [Fact]
    public void Application_Should_Not_Depend_On_Infrastructure_Or_API()
    {
        var result = Types
            .InAssembly(typeof(RegisterDeviceCommandHandler).Assembly)
            .ShouldNot()
            .HaveDependencyOnAny(
                "Infrastructure",
                "API")
            .GetResult();

        Assert.True(result.IsSuccessful, "Application layer must not depend on Infrastructure or API.");
    }

    [Fact]
    public void Infrastructure_Should_Not_Depend_On_API()
    {
        var result = Types
            .InAssembly(typeof(InMemoryEventStore).Assembly)
            .ShouldNot()
            .HaveDependencyOn("API")
            .GetResult();

        Assert.True(result.IsSuccessful, "Infrastructure layer must not depend on API.");
    }
}
