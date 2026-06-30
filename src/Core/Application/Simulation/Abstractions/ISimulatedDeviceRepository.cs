using Application.Simulation.Models;

namespace Application.Simulation.Abstractions;

public interface ISimulatedDeviceRepository
{
    Task<IReadOnlyList<SimulatedDeviceView>> GetAllAsync(CancellationToken cancellationToken);

    Task<SimulatedDeviceView?> GetAsync(Guid deviceId, CancellationToken cancellationToken);

    Task UpsertAsync(SimulatedDeviceView view, CancellationToken cancellationToken);

    Task ResetAsync(CancellationToken cancellationToken);
}
