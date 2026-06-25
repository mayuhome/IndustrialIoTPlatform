using IndustrialIoTPlatform.Domain;
using IndustrialIoTPlatform.Domain.Events;
using IndustrialIoTPlatform.Domain.Exceptions;

namespace IndustrialIoTPlatform.Domain.Tests;

public class UnitTest1
{
    [Fact]
    public void Register_Should_Raise_DeviceRegistered_Event()
    {
        var device = Device.Register(Guid.NewGuid(), "D-001", 120);

        var events = device.DequeueUncommittedEvents();

        Assert.Single(events);
        Assert.IsType<DeviceRegistered>(events.Single());
    }

    [Fact]
    public void Start_Should_Throw_When_Temperature_Too_High()
    {
        var device = Device.Register(Guid.NewGuid(), "D-002", 50);
        _ = device.DequeueUncommittedEvents();
        device.UpdateTemperature(80);

        var act = () => device.Start();

        var exception = Assert.Throws<DomainException>(act);
        Assert.Contains("Temperature is too high", exception.Message);
    }
}
