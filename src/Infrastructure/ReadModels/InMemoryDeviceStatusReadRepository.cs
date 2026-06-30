using System.Collections.Concurrent;
using Application.Abstractions;
using Application.Devices.Models;

namespace Infrastructure.ReadModels;

public sealed class InMemoryDeviceStatusReadRepository : IDeviceStatusReadRepository
{
    private readonly ConcurrentDictionary<Guid, DeviceStatusView> _views = new();

    public Task<IReadOnlyList<DeviceStatusView>> GetAllAsync(CancellationToken cancellationToken)
    {
        var items = _views.Values
            .OrderBy(x => x.DeviceCode)
            .ToArray();

        return Task.FromResult<IReadOnlyList<DeviceStatusView>>(items);
    }

    public Task<DeviceStatusView?> GetAsync(Guid deviceId, CancellationToken cancellationToken)
    {
        _views.TryGetValue(deviceId, out var view);
        return Task.FromResult(view);
    }

    public Task UpsertAsync(DeviceStatusView view, CancellationToken cancellationToken)
    {
        _views[view.DeviceId] = view;
        return Task.CompletedTask;
    }
}
