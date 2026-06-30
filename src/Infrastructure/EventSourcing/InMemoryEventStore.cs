using System.Collections.Concurrent;
using Application.Abstractions;
using Domain.Abstractions;

namespace Infrastructure.EventSourcing;

public sealed class InMemoryEventStore : IEventStore
{
    private readonly ConcurrentDictionary<Guid, List<IDomainEvent>> _streams = new();
    private readonly object _sync = new();

    public Task<IReadOnlyList<IDomainEvent>> LoadAsync(Guid streamId, CancellationToken cancellationToken)
    {
        if (_streams.TryGetValue(streamId, out var stream))
        {
            return Task.FromResult<IReadOnlyList<IDomainEvent>>(stream.ToArray());
        }

        return Task.FromResult<IReadOnlyList<IDomainEvent>>(Array.Empty<IDomainEvent>());
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
            var stream = _streams.GetOrAdd(streamId, _ => new List<IDomainEvent>());
            var actualVersion = stream.Count - 1;

            if (actualVersion != expectedVersion)
            {
                throw new EventStoreConcurrencyException(streamId, expectedVersion, actualVersion);
            }

            stream.AddRange(events);
        }

        return Task.CompletedTask;
    }
}
