using Domain.Abstractions;

namespace Domain.Events;

public sealed record DeviceStopped(
    Guid DeviceId,
    DateTime StoppedOnUtc,
    DateTime OccurredOnUtc) : IDomainEvent;
