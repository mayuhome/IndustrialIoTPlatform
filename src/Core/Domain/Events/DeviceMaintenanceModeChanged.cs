using Domain.Abstractions;

namespace Domain.Events;

public sealed record DeviceMaintenanceModeChanged(
    Guid DeviceId,
    bool IsEnabled,
    DateTime ChangedOnUtc,
    DateTime OccurredOnUtc) : IDomainEvent;
