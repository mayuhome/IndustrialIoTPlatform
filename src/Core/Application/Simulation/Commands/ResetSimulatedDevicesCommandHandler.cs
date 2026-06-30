using Application.Simulation.Abstractions;

namespace Application.Simulation.Commands;

public sealed class ResetSimulatedDevicesCommandHandler(ISimulatedDeviceSimulationService service)
{
    private readonly ISimulatedDeviceSimulationService _service = service;

    public Task Handle(ResetSimulatedDevicesCommand command, CancellationToken cancellationToken)
    {
        return _service.ResetAsync(cancellationToken);
    }
}
