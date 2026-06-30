using Application.Abstractions;
using Application.Devices.Models;
using Domain;

namespace Application.Devices.Commands;

public sealed class RegisterDeviceCommandHandler(
    IEventStore eventStore,
    IDeviceStatusReadRepository readRepository)
{
    private readonly IEventStore _eventStore = eventStore;
    private readonly IDeviceStatusReadRepository _readRepository = readRepository;

    public async Task<Guid> Handle(RegisterDeviceCommand command, CancellationToken cancellationToken)
    {
        var deviceId = Guid.NewGuid();
        var aggregate = Device.Register(deviceId, command.DeviceCode, command.MaxTemperatureThreshold);
        var events = aggregate.DequeueUncommittedEvents();

        await _eventStore.AppendAsync(
            deviceId,
            expectedVersion: -1,
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
        return deviceId;
    }
}
