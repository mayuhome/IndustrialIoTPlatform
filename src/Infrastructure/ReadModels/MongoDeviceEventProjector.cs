using System.Text.Json;
using Application.Abstractions;
using Application.Devices.Models;
using Domain.Events;
using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;
using MongoDB.Driver;
using StackExchange.Redis;

namespace Infrastructure.ReadModels;

public sealed class MongoDeviceEventProjector : IDeviceEventProjector
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private readonly IMongoCollection<DeviceStatusProjectionDocument> _collection;
    private readonly IDatabase _cache;
    private readonly TimeSpan _ttl;
    private readonly SemaphoreSlim _initializationLock = new(1, 1);
    private volatile bool _initialized;

    public MongoDeviceEventProjector(
        IMongoClient mongoClient,
        string databaseName,
        IConnectionMultiplexer redis,
        int defaultTtlSeconds)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(databaseName);

        _collection = mongoClient
            .GetDatabase(databaseName)
            .GetCollection<DeviceStatusProjectionDocument>("device_status_views");
        _cache = redis.GetDatabase();
        _ttl = TimeSpan.FromSeconds(defaultTtlSeconds <= 0 ? 300 : defaultTtlSeconds);
    }

    public async Task ProjectAsync(IReadOnlyList<StoredEvent> events, CancellationToken cancellationToken)
    {
        if (events.Count == 0)
        {
            return;
        }

        await EnsureIndexesAsync(cancellationToken);

        foreach (var storedEvent in events
                     .OrderBy(x => x.StreamId)
                     .ThenBy(x => x.Version))
        {
            var current = await _collection
                .Find(x => x.DeviceId == storedEvent.StreamId)
                .FirstOrDefaultAsync(cancellationToken);

            var currentVersion = current?.LastProjectedVersion ?? -1;
            if (currentVersion >= storedEvent.Version)
            {
                continue;
            }

            var next = Apply(storedEvent, current);
            await _collection.ReplaceOneAsync(
                x => x.DeviceId == next.DeviceId,
                next,
                new ReplaceOptions { IsUpsert = true },
                cancellationToken);

            await CacheAsync(next.ToView());
        }
    }

    public async Task ResetAsync(CancellationToken cancellationToken)
    {
        await EnsureIndexesAsync(cancellationToken);

        await _collection.DeleteManyAsync(
            FilterDefinition<DeviceStatusProjectionDocument>.Empty,
            cancellationToken);
    }

    private async Task EnsureIndexesAsync(CancellationToken cancellationToken)
    {
        if (_initialized)
        {
            return;
        }

        await _initializationLock.WaitAsync(cancellationToken);
        try
        {
            if (_initialized)
            {
                return;
            }

            var uniqueDeviceCode = new CreateIndexModel<DeviceStatusProjectionDocument>(
                Builders<DeviceStatusProjectionDocument>.IndexKeys.Ascending(x => x.DeviceCode),
                new CreateIndexOptions { Name = "ix_device_status_views_device_code", Unique = true });

            var projectionVersion = new CreateIndexModel<DeviceStatusProjectionDocument>(
                Builders<DeviceStatusProjectionDocument>.IndexKeys.Ascending(x => x.LastProjectedVersion),
                new CreateIndexOptions { Name = "ix_device_status_views_last_projected_version", Unique = false });

            await _collection.Indexes.CreateManyAsync([uniqueDeviceCode, projectionVersion], cancellationToken);
            _initialized = true;
        }
        finally
        {
            _initializationLock.Release();
        }
    }

    private DeviceStatusProjectionDocument Apply(StoredEvent storedEvent, DeviceStatusProjectionDocument? current)
    {
        return storedEvent.DomainEvent switch
        {
            DeviceRegistered registered => ApplyRegistered(storedEvent.Version, registered, current),
            DeviceStarted started => ApplyStarted(storedEvent.Version, started, current),
            DeviceStopped stopped => ApplyStopped(storedEvent.Version, stopped, current),
            DeviceMaintenanceModeChanged maintenance => ApplyMaintenance(storedEvent.Version, maintenance, current),
            _ => current ?? throw new NotSupportedException($"Unsupported projector event type '{storedEvent.DomainEvent.GetType().Name}'.")
        };
    }

    private static DeviceStatusProjectionDocument ApplyRegistered(
        int version,
        DeviceRegistered domainEvent,
        DeviceStatusProjectionDocument? current)
    {
        return new DeviceStatusProjectionDocument
        {
            DeviceId = domainEvent.DeviceId,
            DeviceCode = domainEvent.DeviceCode,
            Status = "Active",
            LastHeartbeatUtc = domainEvent.OccurredOnUtc,
            MaxTemperatureThreshold = domainEvent.MaxTemperatureThreshold,
            LastProjectedVersion = version
        };
    }

    private static DeviceStatusProjectionDocument ApplyStarted(
        int version,
        DeviceStarted domainEvent,
        DeviceStatusProjectionDocument? current)
    {
        var state = EnsureDocument(current, domainEvent.DeviceId, nameof(DeviceStarted));
        state.Status = "Running";
        state.LastHeartbeatUtc = domainEvent.StartedOnUtc;
        state.LastProjectedVersion = version;
        return state;
    }

    private static DeviceStatusProjectionDocument ApplyStopped(
        int version,
        DeviceStopped domainEvent,
        DeviceStatusProjectionDocument? current)
    {
        var state = EnsureDocument(current, domainEvent.DeviceId, nameof(DeviceStopped));
        state.Status = "Active";
        state.LastHeartbeatUtc = domainEvent.StoppedOnUtc;
        state.LastProjectedVersion = version;
        return state;
    }

    private static DeviceStatusProjectionDocument ApplyMaintenance(
        int version,
        DeviceMaintenanceModeChanged domainEvent,
        DeviceStatusProjectionDocument? current)
    {
        var state = EnsureDocument(current, domainEvent.DeviceId, nameof(DeviceMaintenanceModeChanged));
        state.Status = domainEvent.IsEnabled ? "Maintenance" : "Active";
        state.LastHeartbeatUtc = domainEvent.ChangedOnUtc;
        state.LastProjectedVersion = version;
        return state;
    }

    private static DeviceStatusProjectionDocument EnsureDocument(
        DeviceStatusProjectionDocument? current,
        Guid deviceId,
        string eventName)
    {
        if (current is null)
        {
            throw new InvalidOperationException(
                $"Cannot apply event '{eventName}' for device '{deviceId}' because projection document does not exist.");
        }

        return current;
    }

    private Task CacheAsync(DeviceStatusView view)
    {
        var payload = JsonSerializer.Serialize(view, JsonOptions);
        return _cache.StringSetAsync(BuildCacheKey(view.DeviceId), payload, _ttl);
    }

    private static string BuildCacheKey(Guid deviceId) => $"devices:status:{deviceId}";

    private sealed class DeviceStatusProjectionDocument
    {
        [BsonId]
        [BsonGuidRepresentation(GuidRepresentation.Standard)]
        public Guid DeviceId { get; init; }

        public string DeviceCode { get; set; } = string.Empty;

        public string Status { get; set; } = string.Empty;

        public DateTime LastHeartbeatUtc { get; set; }

        public double MaxTemperatureThreshold { get; set; }

        public int LastProjectedVersion { get; set; }

        public DeviceStatusView ToView()
        {
            return new DeviceStatusView(
                DeviceId,
                DeviceCode,
                Status,
                LastHeartbeatUtc,
                MaxTemperatureThreshold);
        }
    }
}
