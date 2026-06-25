using IndustrialIoTPlatform.Domain.Abstractions;
using IndustrialIoTPlatform.Domain.Enums;
using IndustrialIoTPlatform.Domain.Events;
using IndustrialIoTPlatform.Domain.Exceptions;

namespace IndustrialIoTPlatform.Domain;

public sealed class Device : EventSourcedAggregateRoot
{
    public string DeviceCode { get; private set; } = string.Empty;
    public DeviceStatus Status { get; private set; }
    public double Temperature { get; private set; }
    public double MaxTemperatureThreshold { get; private set; }
    public DateTime LastHeartbeatUtc { get; private set; }

    public static Device Register(Guid id, string deviceCode, double maxTemperatureThreshold)
    {
        if (id == Guid.Empty)
        {
            throw new DomainException("Device id is required.");
        }

        if (string.IsNullOrWhiteSpace(deviceCode))
        {
            throw new DomainException("Device code is required.");
        }

        if (maxTemperatureThreshold <= 0)
        {
            throw new DomainException("Max temperature threshold must be greater than zero.");
        }

        var device = new Device();
        device.Raise(new DeviceRegistered(id, deviceCode.Trim(), maxTemperatureThreshold, DateTime.UtcNow));
        return device;
    }

    public void UpdateTemperature(double temperature)
    {
        Temperature = temperature;
    }

    public void Start()
    {
        if (Status == DeviceStatus.Running)
        {
            throw new DomainException("Device is already running.");
        }

        if (Temperature > MaxTemperatureThreshold)
        {
            throw new DomainException($"Temperature is too high ({Temperature}C). Cannot start.");
        }

        Raise(new DeviceStarted(Id, DateTime.UtcNow, DateTime.UtcNow));
    }

    protected override void Apply(IDomainEvent domainEvent)
    {
        switch (domainEvent)
        {
            case DeviceRegistered e:
                Id = e.DeviceId;
                DeviceCode = e.DeviceCode;
                MaxTemperatureThreshold = e.MaxTemperatureThreshold;
                Status = DeviceStatus.Active;
                LastHeartbeatUtc = e.OccurredOnUtc;
                break;
            case DeviceStarted e:
                Status = DeviceStatus.Running;
                LastHeartbeatUtc = e.StartedOnUtc;
                break;
        }
    }
}