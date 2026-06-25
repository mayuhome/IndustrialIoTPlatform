using IndustrialIoTPlatform.Application.Abstractions;
using IndustrialIoTPlatform.Application.Devices.Models;
using IndustrialIoTPlatform.Domain;

namespace IndustrialIoTPlatform.Application.Devices.Commands;

public sealed class RegisterDeviceCommandHandler
{
    private readonly IEventStore _eventStore;
    private readonly IDeviceStatusReadRepository _readRepository;

    public RegisterDeviceCommandHandler(
        IEventStore eventStore,
        IDeviceStatusReadRepository readRepository)
    {
        _eventStore = eventStore;
        _readRepository = readRepository;
    }

    public async Task<Guid> Handle(RegisterDeviceCommand command, CancellationToken cancellationToken)
    {
        var deviceId = Guid.NewGuid();
        var aggregate = Device.Register(deviceId, command.DeviceCode, command.MaxTemperatureThreshold);
        var events = aggregate.DequeueUncommittedEvents();

        await _eventStore.AppendAsync(deviceId, expectedVersion: -1, events, cancellationToken);

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
