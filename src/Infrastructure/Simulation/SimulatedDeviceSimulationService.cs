using Application.Simulation;
using Application.Abstractions;
using Application.Devices.Models;
using Application.Simulation.Abstractions;
using Application.Simulation.Models;
using Microsoft.Extensions.Options;

namespace Infrastructure.Simulation;

public sealed class SimulatedDeviceSimulationService(
    ISimulatedDeviceRepository repository,
    ISimulatedDeviceStateGenerator generator,
    IDeviceStatusReadRepository deviceStatusReadRepository,
    IOptions<SimulationOptions> options)
    : ISimulatedDeviceSimulationService
{
    private readonly ISimulatedDeviceRepository _repository = repository;
    private readonly ISimulatedDeviceStateGenerator _generator = generator;
    private readonly IDeviceStatusReadRepository _deviceStatusReadRepository = deviceStatusReadRepository;
    private readonly SimulationOptions _options = options.Value;

    public Task<IReadOnlyList<SimulatedDeviceView>> GetAllAsync(CancellationToken cancellationToken)
    {
        return _repository.GetAllAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<Guid>> GetAllDeviceIdsAsync(CancellationToken cancellationToken)
    {
        await EnsureRealDevicesTrackedAsync(cancellationToken);
        return await _repository.GetAllDeviceIdsAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<SimulatedDeviceView>> GenerateAsync(int deviceCount, string? deviceCodePrefix, CancellationToken cancellationToken)
    {
        if (deviceCount <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(deviceCount), "Device count must be greater than zero.");
        }

        var generated = new List<SimulatedDeviceView>(deviceCount);
        for (var index = 0; index < deviceCount; index++)
        {
            var device = _generator.Create(deviceCodePrefix ?? _options.DeviceCodePrefix);
            await _repository.UpsertAsync(device, cancellationToken);
            generated.Add(device);
        }

        return generated;
    }

    public async Task<IReadOnlyList<SimulatedDeviceView>> AdvanceAsync(
        IReadOnlyCollection<Guid> deviceIds,
        CancellationToken cancellationToken)
    {
        IReadOnlyList<SimulatedDeviceView> devices;
        if (deviceIds.Count == 0)
        {
            devices = await _repository.GetAllAsync(cancellationToken);
        }
        else
        {
            devices = await _repository.GetByIdsAsync(deviceIds, cancellationToken);
        }

        if (devices.Count == 0)
        {
            return Array.Empty<SimulatedDeviceView>();
        }

        var updated = new List<SimulatedDeviceView>(devices.Count);
        foreach (var device in devices)
        {
            var next = _generator.Advance(device);
            await _repository.UpsertAsync(next, cancellationToken);
            updated.Add(next);
        }

        return updated;
    }

    public Task ResetAsync(CancellationToken cancellationToken)
    {
        return _repository.ResetAsync(cancellationToken);
    }

    public async Task EnsureSeededAsync(CancellationToken cancellationToken)
    {
        if (!_options.Enabled || _options.AutoSeedCount <= 0)
        {
            await EnsureRealDevicesTrackedAsync(cancellationToken);
            return;
        }

        var devices = await _repository.GetAllAsync(cancellationToken);
        if (devices.Count > 0)
        {
            await EnsureRealDevicesTrackedAsync(cancellationToken);
            return;
        }

        await GenerateAsync(_options.AutoSeedCount, _options.DeviceCodePrefix, cancellationToken);
        await EnsureRealDevicesTrackedAsync(cancellationToken);
    }

    private async Task EnsureRealDevicesTrackedAsync(CancellationToken cancellationToken)
    {
        var actualDevices = await _deviceStatusReadRepository.GetAllAsync(cancellationToken);
        if (actualDevices.Count == 0)
        {
            return;
        }

        var simulatedDevices = await _repository.GetAllAsync(cancellationToken);
        var simulatedById = simulatedDevices.ToDictionary(x => x.DeviceId);

        foreach (var actualDevice in actualDevices)
        {
            if (simulatedById.TryGetValue(actualDevice.DeviceId, out var existing))
            {
                var synchronized = existing with
                {
                    DeviceCode = actualDevice.DeviceCode,
                    Status = NormalizeStatus(actualDevice.Status, existing.Status),
                    MaxTemperatureThreshold = actualDevice.MaxTemperatureThreshold,
                    LastUpdatedUtc = actualDevice.LastHeartbeatUtc > existing.LastUpdatedUtc
                        ? actualDevice.LastHeartbeatUtc
                        : existing.LastUpdatedUtc
                };

                await _repository.UpsertAsync(synchronized, cancellationToken);
                continue;
            }

            await _repository.UpsertAsync(CreateInitialSimulatedView(actualDevice), cancellationToken);
        }
    }

    private static SimulatedDeviceView CreateInitialSimulatedView(DeviceStatusView actualDevice)
    {
        var initialTemperature = Math.Round(Math.Max(0, Math.Min(actualDevice.MaxTemperatureThreshold - 1, 25)), 1);

        return new SimulatedDeviceView(
            actualDevice.DeviceId,
            actualDevice.DeviceCode,
            NormalizeStatus(actualDevice.Status, "Active"),
            initialTemperature,
            actualDevice.MaxTemperatureThreshold,
            actualDevice.LastHeartbeatUtc,
            0);
    }

    private static string NormalizeStatus(string actualStatus, string fallbackStatus)
    {
        return string.IsNullOrWhiteSpace(actualStatus) ? fallbackStatus : actualStatus.Trim();
    }
}
