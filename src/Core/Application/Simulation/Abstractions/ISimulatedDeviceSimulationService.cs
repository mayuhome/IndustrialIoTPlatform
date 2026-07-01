using Application.Simulation.Models;

namespace Application.Simulation.Abstractions;

public interface ISimulatedDeviceSimulationService
{
    Task<IReadOnlyList<SimulatedDeviceView>> GenerateAsync(int deviceCount, string? deviceCodePrefix, CancellationToken cancellationToken);

    Task<IReadOnlyList<SimulatedDeviceView>> GetAllAsync(CancellationToken cancellationToken);

    Task<IReadOnlyList<Guid>> GetAllDeviceIdsAsync(CancellationToken cancellationToken);

    Task<IReadOnlyList<SimulatedDeviceView>> AdvanceAsync(IReadOnlyCollection<Guid> deviceIds, CancellationToken cancellationToken);

    Task ResetAsync(CancellationToken cancellationToken);

    Task EnsureSeededAsync(CancellationToken cancellationToken);
}
