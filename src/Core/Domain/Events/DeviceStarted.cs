using Domain.Abstractions;

namespace Domain.Events;

public sealed record DeviceStarted(
    Guid DeviceId,
    DateTime StartedOnUtc,
    DateTime OccurredOnUtc) : IDomainEvent;
