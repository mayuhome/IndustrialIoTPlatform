using Application.Abstractions;

namespace Application.Devices.Commands;

public sealed record StartDeviceCommand(
	Guid DeviceId,
	double CurrentTemperature,
	Guid CorrelationId,
	Guid? CausationId) : ITraceableCommand
{
	public StartDeviceCommand(Guid DeviceId, double CurrentTemperature)
		: this(DeviceId, CurrentTemperature, Guid.NewGuid(), null)
	{
	}
}
