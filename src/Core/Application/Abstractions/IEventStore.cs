using Domain.Abstractions;
namespace Application.Abstractions;

public interface IEventStore
{
    Task<IReadOnlyList<IDomainEvent>> LoadAsync(Guid streamId, CancellationToken cancellationToken);

    Task<IReadOnlyList<StoredEvent>> LoadAllAsync(CancellationToken cancellationToken);

    Task AppendAsync(
        Guid streamId,
        int expectedVersion,
        IReadOnlyCollection<IDomainEvent> events,
    CommandMetadata? commandMetadata,
        CancellationToken cancellationToken);
}
