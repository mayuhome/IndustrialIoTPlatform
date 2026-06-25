# CSharp Templates

## Domain Event
```csharp
public interface IDomainEvent
{
    DateTime OccurredOnUtc { get; }
}

public sealed record DeviceStarted(Guid DeviceId, DateTime OccurredOnUtc) : IDomainEvent;
```

## Event-sourced Aggregate Root
```csharp
public abstract class EventSourcedAggregateRoot
{
    private readonly List<IDomainEvent> _uncommitted = new();

    public Guid Id { get; protected set; }
    public int Version { get; private set; } = -1;

    public IReadOnlyCollection<IDomainEvent> DequeueUncommittedEvents()
    {
        var copy = _uncommitted.ToArray();
        _uncommitted.Clear();
        return copy;
    }

    public void LoadsFromHistory(IEnumerable<IDomainEvent> history)
    {
        foreach (var e in history)
        {
            Apply(e);
            Version++;
        }
    }

    protected void Raise(IDomainEvent e)
    {
        Apply(e);
        _uncommitted.Add(e);
    }

    protected abstract void Apply(IDomainEvent e);
}
```

## Command + Handler
```csharp
public sealed record StartDeviceCommand(Guid DeviceId);

public interface IEventStore
{
    Task<IReadOnlyList<IDomainEvent>> LoadAsync(Guid streamId, CancellationToken ct);
    Task AppendAsync(Guid streamId, int expectedVersion, IReadOnlyCollection<IDomainEvent> events, CancellationToken ct);
}

public sealed class StartDeviceCommandHandler
{
    private readonly IEventStore _eventStore;

    public StartDeviceCommandHandler(IEventStore eventStore) => _eventStore = eventStore;

    public async Task Handle(StartDeviceCommand cmd, CancellationToken ct)
    {
        var history = await _eventStore.LoadAsync(cmd.DeviceId, ct);
        var aggregate = new DeviceAggregate();
        aggregate.LoadsFromHistory(history);

        aggregate.Start();

        var events = aggregate.DequeueUncommittedEvents();
        await _eventStore.AppendAsync(cmd.DeviceId, aggregate.Version, events, ct);
    }
}
```

## Query + Handler
```csharp
public sealed record GetDeviceStatusQuery(Guid DeviceId);

public sealed record DeviceStatusView(Guid DeviceId, string Status, DateTime LastHeartbeatUtc);

public interface IDeviceStatusReadRepository
{
    Task<DeviceStatusView?> GetAsync(Guid deviceId, CancellationToken ct);
}

public sealed class GetDeviceStatusQueryHandler
{
    private readonly IDeviceStatusReadRepository _repo;

    public GetDeviceStatusQueryHandler(IDeviceStatusReadRepository repo) => _repo = repo;

    public Task<DeviceStatusView?> Handle(GetDeviceStatusQuery query, CancellationToken ct)
        => _repo.GetAsync(query.DeviceId, ct);
}
```

## Projector
```csharp
public sealed class DeviceStatusProjector
{
    private readonly IDeviceStatusProjectionWriter _writer;

    public DeviceStatusProjector(IDeviceStatusProjectionWriter writer) => _writer = writer;

    public Task ProjectAsync(IDomainEvent domainEvent, CancellationToken ct)
    {
        return domainEvent switch
        {
            DeviceStarted e => _writer.UpsertStartedAsync(e.DeviceId, e.OccurredOnUtc, ct),
            _ => Task.CompletedTask
        };
    }
}
```
