namespace Application.Simulation;

public sealed class SimulationOptions
{
    public bool Enabled { get; set; } = true;

    public int AutoSeedCount { get; set; } = 5;

    public string DeviceCodePrefix { get; set; } = "SIM";

    public int UpdateIntervalSeconds { get; set; } = 1;

    public double StartingTemperatureMin { get; set; } = 18;

    public double StartingTemperatureMax { get; set; } = 28;

    public double TemperatureDriftMin { get; set; } = -1.5;

    public double TemperatureDriftMax { get; set; } = 2.5;

    public double MaxTemperatureThresholdMin { get; set; } = 70;

    public double MaxTemperatureThresholdMax { get; set; } = 120;
}
