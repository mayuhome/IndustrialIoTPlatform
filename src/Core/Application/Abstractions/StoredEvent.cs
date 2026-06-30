using Domain.Abstractions;

namespace Application.Abstractions;

public sealed record StoredEvent(
    Guid StreamId,
    int Version,
    IDomainEvent DomainEvent,
    CommandMetadata? CommandMetadata);
