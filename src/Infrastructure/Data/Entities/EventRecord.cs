namespace Infrastructure.Data.Entities;

public sealed class EventRecord
{
    public Guid StreamId { get; set; }

    public int Version { get; set; }

    public string EventType { get; set; } = string.Empty;

    public string Payload { get; set; } = string.Empty;

    public DateTime OccurredOnUtc { get; set; }
}
