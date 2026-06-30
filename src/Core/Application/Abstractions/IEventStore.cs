using Domain.Abstractions;
namespace Application.Abstractions;

public interface IEventStore
{
    Task<IReadOnlyList<IDomainEvent>> LoadAsync(Guid streamId, CancellationToken cancellationToken);

    Task AppendAsync(
        Guid streamId,
        int expectedVersion,
        IReadOnlyCollection<IDomainEvent> events,
        CancellationToken cancellationToken);
}
