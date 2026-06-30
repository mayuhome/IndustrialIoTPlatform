namespace Application.Abstractions;

public static class TraceableCommandExtensions
{
    public static CommandMetadata ToMetadata(this ITraceableCommand command)
    {
        return new CommandMetadata(command.CorrelationId, command.CausationId);
    }
}
