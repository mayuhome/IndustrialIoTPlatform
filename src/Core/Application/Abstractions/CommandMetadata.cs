namespace Application.Abstractions;

public sealed record CommandMetadata(Guid CorrelationId, Guid? CausationId);
