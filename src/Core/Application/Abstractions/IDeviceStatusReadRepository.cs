using Application.Devices.Models;

namespace Application.Abstractions;

public interface IDeviceStatusReadRepository
{
    Task<IReadOnlyList<DeviceStatusView>> GetAllAsync(CancellationToken cancellationToken);

    Task<DeviceStatusView?> GetAsync(Guid deviceId, CancellationToken cancellationToken);

    Task UpsertAsync(DeviceStatusView view, CancellationToken cancellationToken);
}
