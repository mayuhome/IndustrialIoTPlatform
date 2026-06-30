namespace Application.Devices.Commands;

public sealed record StartDeviceCommand(Guid DeviceId, double CurrentTemperature);
