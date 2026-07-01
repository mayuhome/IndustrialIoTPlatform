using Application.Simulation;
using Application.Simulation.Abstractions;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace API.Realtime;

public sealed class SimulationStreamBackgroundService(
    ISimulatedDeviceSimulationService simulationService,
    ISimulationDataBroadcaster broadcaster,
    IOptions<SimulationOptions> options,
    ILogger<SimulationStreamBackgroundService> logger)
    : BackgroundService
{
    private readonly ISimulatedDeviceSimulationService _simulationService = simulationService;
    private readonly ISimulationDataBroadcaster _broadcaster = broadcaster;
    private readonly SimulationOptions _options = options.Value;
    private readonly ILogger<SimulationStreamBackgroundService> _logger = logger;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!_options.Enabled)
        {
            _logger.LogInformation("Simulation stream worker is disabled.");
            return;
        }

        await _simulationService.EnsureSeededAsync(stoppingToken);

        var deviceIds = await _simulationService.GetAllDeviceIdsAsync(stoppingToken);
        _logger.LogInformation("Simulation stream worker initialized with {DeviceCount} device ids.", deviceIds.Count);

        var interval = TimeSpan.FromSeconds(Math.Max(1, _options.UpdateIntervalSeconds));
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                var generated = await _simulationService.AdvanceAsync(deviceIds, stoppingToken);
                if (generated.Count > 0)
                {
                    await _broadcaster.BroadcastAsync(generated, stoppingToken);
                }
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Simulation stream worker failed during tick.");
            }

            await Task.Delay(interval, stoppingToken);
        }
    }
}
