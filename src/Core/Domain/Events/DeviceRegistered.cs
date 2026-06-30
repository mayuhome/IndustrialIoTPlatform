using Domain.Abstractions;

namespace Domain.Events;

public sealed record DeviceRegistered(
    Guid DeviceId,
    string DeviceCode,
    double MaxTemperatureThreshold,
    DateTime OccurredOnUtc) : IDomainEvent;
