using Application.Simulation.Abstractions;
using Application.Simulation.Models;
using Microsoft.Extensions.Options;

namespace Application.Simulation;

public sealed class SimulatedDeviceSimulationService(
    ISimulatedDeviceRepository repository,
    IOptions<SimulationOptions> options)
    : ISimulatedDeviceSimulationService
{
    private readonly ISimulatedDeviceRepository _repository = repository;
    private readonly SimulationOptions _options = options.Value;

    public Task<IReadOnlyList<SimulatedDeviceView>> GetAllAsync(CancellationToken cancellationToken)
    {
        return _repository.GetAllAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<SimulatedDeviceView>> GenerateAsync(
        int deviceCount,
        string? deviceCodePrefix,
        CancellationToken cancellationToken)
    {
        if (deviceCount <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(deviceCount), "Device count must be greater than zero.");
        }

        var prefix = NormalizePrefix(deviceCodePrefix);
        var batchId = DateTime.UtcNow.ToString("yyyyMMddHHmmss");
        var generated = new List<SimulatedDeviceView>(deviceCount);

        for (var index = 0; index < deviceCount; index++)
        {
            var threshold = NextDouble(_options.MaxTemperatureThresholdMin, _options.MaxTemperatureThresholdMax);
            var temperature = NextDouble(_options.StartingTemperatureMin, Math.Min(_options.StartingTemperatureMax, threshold - 1));

            var view = new SimulatedDeviceView(
                Guid.NewGuid(),
                $"{prefix}-{batchId}-{index + 1:0000}",
                "Active",
                temperature,
                threshold,
                DateTime.UtcNow,
                0);

            await _repository.UpsertAsync(view, cancellationToken);
            generated.Add(view);
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

        var updated = new List<SimulatedDeviceView>(devices.Count);
        foreach (var device in devices)
        {
            var next = Advance(device);
            await _repository.UpsertAsync(next, cancellationToken);
            updated.Add(next);
        }

        return updated.Count;
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

    private SimulatedDeviceView Advance(SimulatedDeviceView current)
    {
        var delta = NextDouble(_options.TemperatureDriftMin, _options.TemperatureDriftMax);
        var temperature = Math.Round(Math.Clamp(current.CurrentTemperature + delta, 0, 200), 1);
        var status = temperature > current.MaxTemperatureThreshold ? "Error" : "Running";

        return current with
        {
            CurrentTemperature = temperature,
            Status = status,
            LastUpdatedUtc = DateTime.UtcNow,
            Sequence = current.Sequence + 1
        };
    }

    private static string NormalizePrefix(string? prefix)
    {
        return string.IsNullOrWhiteSpace(prefix) ? "SIM" : prefix.Trim().ToUpperInvariant();
    }

    private static double NextDouble(double min, double max)
    {
        if (max <= min)
        {
            return Math.Round(min, 1);
        }

        return Math.Round(Random.Shared.NextDouble() * (max - min) + min, 1);
    }
}
