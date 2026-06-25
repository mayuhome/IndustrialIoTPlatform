using IndustrialIoTPlatform.Application.Devices.Models;

namespace IndustrialIoTPlatform.Application.Abstractions;

public interface IDeviceStatusReadRepository
{
    Task<DeviceStatusView?> GetAsync(Guid deviceId, CancellationToken cancellationToken);

    Task UpsertAsync(DeviceStatusView view, CancellationToken cancellationToken);
}
