using Application.Simulation.Models;

namespace API.Realtime;

public interface ISimulationDataBroadcaster
{
    Task BroadcastAsync(IReadOnlyList<SimulatedDeviceView> views, CancellationToken cancellationToken);
}
