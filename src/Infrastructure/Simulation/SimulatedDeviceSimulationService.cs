using Application.Simulation.Abstractions;
using Application.Simulation.Models;
using Application.Simulation;
using Microsoft.Extensions.Options;

namespace Infrastructure.Simulation;

public sealed class SimulatedDeviceSimulationService(
    ISimulatedDeviceRepository repository,
    ISimulatedDeviceStateGenerator generator,
    IOptions<SimulationOptions> options)
    : ISimulatedDeviceSimulationService
{
    private readonly ISimulatedDeviceRepository _repository = repository;
    private readonly ISimulatedDeviceStateGenerator _generator = generator;
    private readonly SimulationOptions _options = options.Value;

    public Task<IReadOnlyList<SimulatedDeviceView>> GetAllAsync(CancellationToken cancellationToken)
    {
        return _repository.GetAllAsync(cancellationToken);
    }

    public Task<IReadOnlyList<Guid>> GetAllDeviceIdsAsync(CancellationToken cancellationToken)
    {
        return _repository.GetAllDeviceIdsAsync(cancellationToken);
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
            return;
        }

        var devices = await _repository.GetAllAsync(cancellationToken);
        if (devices.Count > 0)
        {
            return;
        }

        await GenerateAsync(_options.AutoSeedCount, _options.DeviceCodePrefix, cancellationToken);
    }
}
