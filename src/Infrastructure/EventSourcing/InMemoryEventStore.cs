using System.Collections.Concurrent;
using Application.Abstractions;
using Domain.Abstractions;

namespace Infrastructure.EventSourcing;

public sealed class InMemoryEventStore : IEventStore
{
    private readonly ConcurrentDictionary<Guid, List<StoredEvent>> _streams = new();
    private readonly object _sync = new();

    public Task<IReadOnlyList<IDomainEvent>> LoadAsync(Guid streamId, CancellationToken cancellationToken)
    {
        if (_streams.TryGetValue(streamId, out var stream))
        {
            return Task.FromResult<IReadOnlyList<IDomainEvent>>(stream.Select(x => x.DomainEvent).ToArray());
        }

        return Task.FromResult<IReadOnlyList<IDomainEvent>>(Array.Empty<IDomainEvent>());
    }

    public Task<IReadOnlyList<StoredEvent>> LoadAllAsync(CancellationToken cancellationToken)
    {
        var all = _streams.Values
            .SelectMany(x => x)
            .OrderBy(x => x.StreamId)
            .ThenBy(x => x.Version)
            .ToArray();

        return Task.FromResult<IReadOnlyList<StoredEvent>>(all);
    }

    public Task AppendAsync(
        Guid streamId,
        int expectedVersion,
        IReadOnlyCollection<IDomainEvent> events,
        CommandMetadata? commandMetadata,
        CancellationToken cancellationToken)
    {
        lock (_sync)
        {
            var stream = _streams.GetOrAdd(streamId, _ => new List<StoredEvent>());
            var actualVersion = stream.Count - 1;

            if (actualVersion != expectedVersion)
            {
                throw new EventStoreConcurrencyException(streamId, expectedVersion, actualVersion);
            }

            var nextVersion = expectedVersion + 1;
            foreach (var domainEvent in events)
            {
                stream.Add(new StoredEvent(streamId, nextVersion, domainEvent, commandMetadata));
                nextVersion++;
            }
        }

        return Task.CompletedTask;
    }
}
