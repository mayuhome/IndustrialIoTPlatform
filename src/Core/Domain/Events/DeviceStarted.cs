using IndustrialIoTPlatform.Domain.Abstractions;

namespace IndustrialIoTPlatform.Domain.Events;

public sealed record DeviceStarted(
    Guid DeviceId,
    DateTime StartedOnUtc,
    DateTime OccurredOnUtc) : IDomainEvent;
