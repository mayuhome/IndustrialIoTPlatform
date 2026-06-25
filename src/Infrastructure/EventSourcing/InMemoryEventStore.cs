using System.Collections.Concurrent;
using IndustrialIoTPlatform.Application.Abstractions;
using IndustrialIoTPlatform.Domain.Abstractions;

namespace IndustrialIoTPlatform.Infrastructure.EventSourcing;

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
        CancellationToken cancellationToken)
    {
        lock (_sync)
        {
            var stream = _streams.GetOrAdd(streamId, _ => new List<IDomainEvent>());
            var actualVersion = stream.Count - 1;

            if (actualVersion != expectedVersion)
            {
                throw new InvalidOperationException(
                    $"Concurrency conflict for stream '{streamId}'. Expected version {expectedVersion}, actual version {actualVersion}.");
            }

            stream.AddRange(events);
        }

        return Task.CompletedTask;
    }
}
