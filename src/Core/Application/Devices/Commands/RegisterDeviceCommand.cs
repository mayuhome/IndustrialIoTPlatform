namespace IndustrialIoTPlatform.Application.Devices.Commands;

public sealed record RegisterDeviceCommand(string DeviceCode, double MaxTemperatureThreshold);
