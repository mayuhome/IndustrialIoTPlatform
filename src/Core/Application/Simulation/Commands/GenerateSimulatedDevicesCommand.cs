namespace Application.Simulation.Commands;

public sealed record GenerateSimulatedDevicesCommand(int DeviceCount, string? DeviceCodePrefix);
