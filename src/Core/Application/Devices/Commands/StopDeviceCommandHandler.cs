using Application.Abstractions;
using Domain;

namespace Application.Devices.Commands;

public sealed class StopDeviceCommandHandler(
    IEventStore eventStore,
    IDeviceEventProjector projector)
{
    private readonly IEventStore _eventStore = eventStore;
    private readonly IDeviceEventProjector _projector = projector;

    public async Task Handle(StopDeviceCommand command, CancellationToken cancellationToken)
    {
        var history = await _eventStore.LoadAsync(command.DeviceId, cancellationToken);
        if (history.Count == 0)
        {
            throw new InvalidOperationException($"Device '{command.DeviceId}' does not exist.");
        }

        var aggregate = new Device();
        aggregate.LoadFromHistory(history);
        aggregate.Stop();

        var events = aggregate.DequeueUncommittedEvents();
        var expectedVersion = aggregate.Version;
        var metadata = command.ToMetadata();

        await _eventStore.AppendAsync(
            command.DeviceId,
            expectedVersion,
            events,
            metadata,
            cancellationToken);

        var projectedEvents = events
            .Select((domainEvent, index) => new StoredEvent(command.DeviceId, expectedVersion + index + 1, domainEvent, metadata))
            .ToArray();

        await _projector.ProjectAsync(projectedEvents, cancellationToken);
    }
}
