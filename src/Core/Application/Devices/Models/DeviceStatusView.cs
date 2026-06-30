namespace Application.Devices.Models;

public sealed record DeviceStatusView(
    Guid DeviceId,
    string DeviceCode,
    string Status,
    DateTime LastHeartbeatUtc,
    double MaxTemperatureThreshold);
