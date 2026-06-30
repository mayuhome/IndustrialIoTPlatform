using Application.Simulation.Abstractions;
using Application.Simulation.Models;
using Application.Simulation;
using Microsoft.Extensions.Options;

namespace Infrastructure.Simulation;

public sealed class DefaultSimulatedDeviceStateGenerator(IOptions<SimulationOptions> options) : ISimulatedDeviceStateGenerator
{
    private readonly SimulationOptions _options = options.Value;

    public SimulatedDeviceView Create(string deviceCodePrefix)
    {
        var prefix = string.IsNullOrWhiteSpace(deviceCodePrefix) ? _options.DeviceCodePrefix : deviceCodePrefix.Trim().ToUpperInvariant();
        var deviceId = Guid.NewGuid();
        var threshold = NextDouble(_options.MaxTemperatureThresholdMin, _options.MaxTemperatureThresholdMax);
        var temperature = NextDouble(_options.StartingTemperatureMin, Math.Min(_options.StartingTemperatureMax, threshold - 1));

        return new SimulatedDeviceView(
            deviceId,
            $"{prefix}-{DateTime.UtcNow:yyyyMMddHHmmss}-{Random.Shared.Next(1000, 9999)}",
            temperature > threshold ? "Error" : "Active",
            temperature,
            threshold,
            DateTime.UtcNow,
            0);
    }

    public SimulatedDeviceView Advance(SimulatedDeviceView current)
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

    private static double NextDouble(double min, double max)
    {
        if (max <= min)
        {
            return Math.Round(min, 1);
        }

        return Math.Round(Random.Shared.NextDouble() * (max - min) + min, 1);
    }
}
