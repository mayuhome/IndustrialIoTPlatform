using Application.Abstractions;

namespace Application.Devices.Commands;

public sealed record RegisterDeviceCommand(
	string DeviceCode,
	double MaxTemperatureThreshold,
	Guid CorrelationId,
	Guid? CausationId) : ITraceableCommand
{
	public RegisterDeviceCommand(string DeviceCode, double MaxTemperatureThreshold)
		: this(DeviceCode, MaxTemperatureThreshold, Guid.NewGuid(), null)
	{
	}
}
