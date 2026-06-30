using Application.Simulation.Abstractions;
using Application.Simulation.Models;

namespace Application.Simulation.Commands;

public sealed class GenerateSimulatedDevicesCommandHandler(ISimulatedDeviceSimulationService service)
{
    private readonly ISimulatedDeviceSimulationService _service = service;

    public Task<IReadOnlyList<SimulatedDeviceView>> Handle(
        GenerateSimulatedDevicesCommand command,
        CancellationToken cancellationToken)
    {
        return _service.GenerateAsync(command.DeviceCount, command.DeviceCodePrefix, cancellationToken);
    }
}
