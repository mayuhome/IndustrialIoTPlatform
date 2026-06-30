namespace Application.Abstractions;

public sealed class EventStoreConcurrencyException : Exception
{
    public Guid StreamId { get; }

    public int ExpectedVersion { get; }

    public int? ActualVersion { get; }

    public EventStoreConcurrencyException(
        Guid streamId,
        int expectedVersion,
        int? actualVersion,
        Exception? innerException = null)
        : base(BuildMessage(streamId, expectedVersion, actualVersion), innerException)
    {
        StreamId = streamId;
        ExpectedVersion = expectedVersion;
        ActualVersion = actualVersion;
    }

    private static string BuildMessage(Guid streamId, int expectedVersion, int? actualVersion)
    {
        return actualVersion.HasValue
            ? $"Concurrency conflict for stream '{streamId}'. Expected version {expectedVersion}, actual version {actualVersion.Value}."
            : $"Concurrency conflict for stream '{streamId}'. Expected version {expectedVersion}, actual version is unknown.";
    }
}
