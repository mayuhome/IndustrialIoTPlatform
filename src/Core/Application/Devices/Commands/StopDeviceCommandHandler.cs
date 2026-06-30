using Application.Abstractions;
using Application.Devices.Models;
using Domain;

namespace Application.Devices.Commands;

public sealed class StopDeviceCommandHandler(
    IEventStore eventStore,
    IDeviceStatusReadRepository readRepository)
{
    private readonly IEventStore _eventStore = eventStore;
    private readonly IDeviceStatusReadRepository _readRepository = readRepository;

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
        await _eventStore.AppendAsync(
            command.DeviceId,
            aggregate.Version,
            events,
            command.ToMetadata(),
            cancellationToken);

        var view = new DeviceStatusView(
            aggregate.Id,
            aggregate.DeviceCode,
            aggregate.Status.ToString(),
            aggregate.LastHeartbeatUtc,
            aggregate.MaxTemperatureThreshold);

        await _readRepository.UpsertAsync(view, cancellationToken);
    }
}
