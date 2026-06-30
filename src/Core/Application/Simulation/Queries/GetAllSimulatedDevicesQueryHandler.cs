using Application.Simulation.Abstractions;
using Application.Simulation.Models;

namespace Application.Simulation.Queries;

public sealed class GetAllSimulatedDevicesQueryHandler(ISimulatedDeviceSimulationService service)
{
    private readonly ISimulatedDeviceSimulationService _service = service;

    public Task<IReadOnlyList<SimulatedDeviceView>> Handle(GetAllSimulatedDevicesQuery query, CancellationToken cancellationToken)
    {
        return _service.GetAllAsync(cancellationToken);
    }
}
