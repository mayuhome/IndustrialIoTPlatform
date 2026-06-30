namespace Application.Abstractions;

public interface ITraceableCommand
{
    Guid CorrelationId { get; }

    Guid? CausationId { get; }
}
