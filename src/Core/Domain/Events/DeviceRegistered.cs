using IndustrialIoTPlatform.Domain.Abstractions;

namespace IndustrialIoTPlatform.Domain.Events;

public sealed record DeviceRegistered(
    Guid DeviceId,
    string DeviceCode,
    double MaxTemperatureThreshold,
    DateTime OccurredOnUtc) : IDomainEvent;
