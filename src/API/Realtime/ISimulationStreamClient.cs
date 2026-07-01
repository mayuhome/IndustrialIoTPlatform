namespace API.Realtime;

public interface ISimulationStreamClient
{
    Task ReceiveSimulationData(SimulationDataPoint dataPoint);
}
