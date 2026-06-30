using Application.Abstractions;

namespace Application.Devices.Commands;

public sealed record StopDeviceCommand(
    Guid DeviceId,
    Guid CorrelationId,
    Guid? CausationId) : ITraceableCommand
{
    public StopDeviceCommand(Guid DeviceId)
        : this(DeviceId, Guid.NewGuid(), null)
    {
    }
}
