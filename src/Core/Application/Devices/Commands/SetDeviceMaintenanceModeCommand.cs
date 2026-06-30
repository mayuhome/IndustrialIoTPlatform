using Application.Abstractions;

namespace Application.Devices.Commands;

public sealed record SetDeviceMaintenanceModeCommand(
    Guid DeviceId,
    bool IsEnabled,
    Guid CorrelationId,
    Guid? CausationId) : ITraceableCommand
{
    public SetDeviceMaintenanceModeCommand(Guid DeviceId, bool IsEnabled)
        : this(DeviceId, IsEnabled, Guid.NewGuid(), null)
    {
    }
}
