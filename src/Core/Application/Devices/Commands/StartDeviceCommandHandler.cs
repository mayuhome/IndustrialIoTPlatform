using Application.Abstractions;
using Domain;

namespace Application.Devices.Commands;

public sealed class StartDeviceCommandHandler
{
    private readonly IEventStore _eventStore;
    private readonly IDeviceEventProjector _projector;

    public StartDeviceCommandHandler(
        IEventStore eventStore,
        IDeviceEventProjector projector)
    {
        _eventStore = eventStore;
        _projector = projector;
    }

    public async Task Handle(StartDeviceCommand command, CancellationToken cancellationToken)
    {
        var history = await _eventStore.LoadAsync(command.DeviceId, cancellationToken);
        if (history.Count == 0)
        {
            throw new InvalidOperationException($"Device '{command.DeviceId}' does not exist.");
        }

        var aggregate = new Device();
        aggregate.LoadFromHistory(history);
        aggregate.UpdateTemperature(command.CurrentTemperature);
        aggregate.Start();

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
