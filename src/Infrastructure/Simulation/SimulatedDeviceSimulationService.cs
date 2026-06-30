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

    public async Task<int> AdvanceAsync(CancellationToken cancellationToken)
    {
        var devices = await _repository.GetAllAsync(cancellationToken);
        if (devices.Count == 0)
        {
            return 0;
        }

        foreach (var device in devices)
        {
            await _repository.UpsertAsync(_generator.Advance(device), cancellationToken);
        }

        return devices.Count;
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
