using Application.Simulation;
using Application.Simulation.Abstractions;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Infrastructure.Simulation;

public sealed class SimulationBackgroundService(
    ISimulatedDeviceSimulationService simulationService,
    IOptions<SimulationOptions> options,
    ILogger<SimulationBackgroundService> logger)
    : BackgroundService
{
    private readonly ISimulatedDeviceSimulationService _simulationService = simulationService;
    private readonly SimulationOptions _options = options.Value;
    private readonly ILogger<SimulationBackgroundService> _logger = logger;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!_options.Enabled)
        {
            _logger.LogInformation("Simulation background worker is disabled.");
            return;
        }

        await simulationService.EnsureSeededAsync(stoppingToken);

        var interval = TimeSpan.FromSeconds(Math.Max(1, _options.UpdateIntervalSeconds));
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                var updated = await _simulationService.AdvanceAsync(stoppingToken);
                _logger.LogInformation("Simulation worker advanced {UpdatedCount} devices.", updated);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Simulation worker failed to advance devices.");
            }

            await Task.Delay(interval, stoppingToken);
        }
    }
}
