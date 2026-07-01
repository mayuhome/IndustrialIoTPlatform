using Application.Simulation.Models;
using Microsoft.AspNetCore.SignalR;

namespace API.Realtime;

public sealed class SignalRSimulationDataBroadcaster(IHubContext<SimulationHub, ISimulationStreamClient> hubContext)
    : ISimulationDataBroadcaster
{
    private readonly IHubContext<SimulationHub, ISimulationStreamClient> _hubContext = hubContext;

    public async Task BroadcastAsync(IReadOnlyList<SimulatedDeviceView> views, CancellationToken cancellationToken)
    {
        if (views.Count == 0)
        {
            return;
        }

        foreach (var view in views)
        {
            var dataPoint = new SimulationDataPoint(
                view.DeviceId,
                view.DeviceCode,
                view.Status,
                view.CurrentTemperature,
                view.MaxTemperatureThreshold,
                view.LastUpdatedUtc,
                view.Sequence);

            await _hubContext.Clients.All.ReceiveSimulationData(dataPoint);
        }
    }
}
