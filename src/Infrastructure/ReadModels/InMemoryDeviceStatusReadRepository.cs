using System.Collections.Concurrent;
using IndustrialIoTPlatform.Application.Abstractions;
using IndustrialIoTPlatform.Application.Devices.Models;

namespace IndustrialIoTPlatform.Infrastructure.ReadModels;

public sealed class InMemoryDeviceStatusReadRepository : IDeviceStatusReadRepository
{
    private readonly ConcurrentDictionary<Guid, DeviceStatusView> _views = new();

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
