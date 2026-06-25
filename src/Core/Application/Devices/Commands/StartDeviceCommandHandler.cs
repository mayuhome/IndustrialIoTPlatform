using IndustrialIoTPlatform.Application.Abstractions;
using IndustrialIoTPlatform.Application.Devices.Models;
using IndustrialIoTPlatform.Domain;

namespace IndustrialIoTPlatform.Application.Devices.Commands;

public sealed class StartDeviceCommandHandler
{
    private readonly IEventStore _eventStore;
    private readonly IDeviceStatusReadRepository _readRepository;

    public StartDeviceCommandHandler(
        IEventStore eventStore,
        IDeviceStatusReadRepository readRepository)
    {
        _eventStore = eventStore;
        _readRepository = readRepository;
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
        await _eventStore.AppendAsync(command.DeviceId, aggregate.Version, events, cancellationToken);

        var view = new DeviceStatusView(
            aggregate.Id,
            aggregate.DeviceCode,
            aggregate.Status.ToString(),
            aggregate.LastHeartbeatUtc,
            aggregate.MaxTemperatureThreshold);

        await _readRepository.UpsertAsync(view, cancellationToken);
    }
}
