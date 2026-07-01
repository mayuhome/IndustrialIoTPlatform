namespace API.Realtime;

public sealed record SimulationDataPoint(
    Guid DeviceId,
    string DeviceCode,
    string Status,
    double CurrentTemperature,
    double MaxTemperatureThreshold,
    DateTime LastUpdatedUtc,
    long Sequence);
