using NetArchTest.Rules;

namespace IndustrialIoTPlatform.Architecture.Tests;

public class UnitTest1
{
    [Fact]
    public void Domain_Should_Not_Depend_On_Other_Layers()
    {
        var result = Types
            .InAssembly(typeof(IndustrialIoTPlatform.Domain.Class1).Assembly)
            .ShouldNot()
            .HaveDependencyOnAny(
                "IndustrialIoTPlatform.Application",
                "IndustrialIoTPlatform.Infrastructure",
                "IndustrialIoTPlatform.API")
            .GetResult();

        Assert.True(result.IsSuccessful, "Domain layer must not depend on outer layers.");
    }

    [Fact]
    public void Application_Should_Not_Depend_On_Infrastructure_Or_API()
    {
        var result = Types
            .InAssembly(typeof(IndustrialIoTPlatform.Application.Class1).Assembly)
            .ShouldNot()
            .HaveDependencyOnAny(
                "IndustrialIoTPlatform.Infrastructure",
                "IndustrialIoTPlatform.API")
            .GetResult();

        Assert.True(result.IsSuccessful, "Application layer must not depend on Infrastructure or API.");
    }

    [Fact]
    public void Infrastructure_Should_Not_Depend_On_API()
    {
        var result = Types
            .InAssembly(typeof(IndustrialIoTPlatform.Infrastructure.Class1).Assembly)
            .ShouldNot()
            .HaveDependencyOn("IndustrialIoTPlatform.API")
            .GetResult();

        Assert.True(result.IsSuccessful, "Infrastructure layer must not depend on API.");
    }
}
