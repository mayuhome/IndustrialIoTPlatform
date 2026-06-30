using Application.Simulation.Models;

namespace Application.Simulation.Abstractions;

public interface ISimulatedDeviceStateGenerator
{
    SimulatedDeviceView Create(string deviceCodePrefix);

    SimulatedDeviceView Advance(SimulatedDeviceView current);
}
