using Application.Abstractions;
using Domain;

namespace Application.Devices.Commands;

public sealed class RegisterDeviceCommandHandler(
    IEventStore eventStore,
    IDeviceEventProjector projector)
{
    private readonly IEventStore _eventStore = eventStore;
    private readonly IDeviceEventProjector _projector = projector;

    public async Task<Guid> Handle(RegisterDeviceCommand command, CancellationToken cancellationToken)
    {
        var deviceId = Guid.NewGuid();
        var aggregate = Device.Register(deviceId, command.DeviceCode, command.MaxTemperatureThreshold);
        var events = aggregate.DequeueUncommittedEvents();
        var expectedVersion = -1;
        var metadata = command.ToMetadata();

        await _eventStore.AppendAsync(
            deviceId,
            expectedVersion,
            events,
            metadata,
            cancellationToken);

        var projectedEvents = events
            .Select((domainEvent, index) => new StoredEvent(deviceId, expectedVersion + index + 1, domainEvent, metadata))
            .ToArray();

        await _projector.ProjectAsync(projectedEvents, cancellationToken);
        return deviceId;
    }
}
