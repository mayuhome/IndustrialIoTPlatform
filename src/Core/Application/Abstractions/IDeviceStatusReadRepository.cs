using Application.Devices.Models;

namespace Application.Abstractions;

public interface IDeviceStatusReadRepository
{
    Task<DeviceStatusView?> GetAsync(Guid deviceId, CancellationToken cancellationToken);

    Task UpsertAsync(DeviceStatusView view, CancellationToken cancellationToken);
}
