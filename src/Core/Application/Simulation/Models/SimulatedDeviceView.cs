namespace Application.Simulation.Models;

public sealed record SimulatedDeviceView(
    Guid DeviceId,
    string DeviceCode,
    string Status,
    double CurrentTemperature,
    double MaxTemperatureThreshold,
    DateTime LastUpdatedUtc,
    long Sequence);
