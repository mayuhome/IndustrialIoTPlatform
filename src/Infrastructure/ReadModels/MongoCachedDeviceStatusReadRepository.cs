using System.Text.Json;
using Application.Abstractions;
using Application.Devices.Models;
using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;
using MongoDB.Driver;
using StackExchange.Redis;

namespace Infrastructure.ReadModels;

public sealed class MongoCachedDeviceStatusReadRepository : IDeviceStatusReadRepository
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private readonly IMongoCollection<DeviceStatusDocument> _collection;
    private readonly IDatabase _cache;
    private readonly TimeSpan _ttl;
    private readonly SemaphoreSlim _initializationLock = new(1, 1);
    private volatile bool _initialized;

    public MongoCachedDeviceStatusReadRepository(
        IMongoClient mongoClient,
        string databaseName,
        IConnectionMultiplexer redis,
        int defaultTtlSeconds)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(databaseName);

        _collection = mongoClient
            .GetDatabase(databaseName)
            .GetCollection<DeviceStatusDocument>("device_status_views");
        _cache = redis.GetDatabase();
        _ttl = TimeSpan.FromSeconds(defaultTtlSeconds <= 0 ? 300 : defaultTtlSeconds);
    }

    public async Task<DeviceStatusView?> GetAsync(Guid deviceId, CancellationToken cancellationToken)
    {
        await EnsureIndexesAsync(cancellationToken);

        var cacheKey = BuildCacheKey(deviceId);
        var cached = await _cache.StringGetAsync(cacheKey);
        if (cached.HasValue)
        {
            return JsonSerializer.Deserialize<DeviceStatusView>(cached.ToString(), JsonOptions);
        }

        var document = await _collection
            .Find(x => x.DeviceId == deviceId)
            .FirstOrDefaultAsync(cancellationToken);

        if (document is null)
        {
            return null;
        }

        var view = document.ToView();
        await CacheAsync(cacheKey, view);
        return view;
    }

    public async Task UpsertAsync(DeviceStatusView view, CancellationToken cancellationToken)
    {
        await EnsureIndexesAsync(cancellationToken);

        var document = DeviceStatusDocument.FromView(view);
        await _collection.ReplaceOneAsync(
            x => x.DeviceId == view.DeviceId,
            document,
            new ReplaceOptions { IsUpsert = true },
            cancellationToken);

        await CacheAsync(BuildCacheKey(view.DeviceId), view);
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

            var index = new CreateIndexModel<DeviceStatusDocument>(
                Builders<DeviceStatusDocument>.IndexKeys.Ascending(x => x.DeviceCode),
                new CreateIndexOptions { Name = "ix_device_status_views_device_code", Unique = true });

            await _collection.Indexes.CreateOneAsync(index, cancellationToken: cancellationToken);
            _initialized = true;
        }
        finally
        {
            _initializationLock.Release();
        }
    }

    private Task CacheAsync(string cacheKey, DeviceStatusView view)
    {
        var payload = JsonSerializer.Serialize(view, JsonOptions);
        return _cache.StringSetAsync(cacheKey, payload, _ttl);
    }

    private static string BuildCacheKey(Guid deviceId) => $"devices:status:{deviceId}";

    private sealed class DeviceStatusDocument
    {
        [BsonId]
        [BsonGuidRepresentation(GuidRepresentation.Standard)]
        public Guid DeviceId { get; init; }

        public string DeviceCode { get; init; } = string.Empty;

        public string Status { get; init; } = string.Empty;

        public DateTime LastHeartbeatUtc { get; init; }

        public double MaxTemperatureThreshold { get; init; }

        public static DeviceStatusDocument FromView(DeviceStatusView view)
        {
            return new DeviceStatusDocument
            {
                DeviceId = view.DeviceId,
                DeviceCode = view.DeviceCode,
                Status = view.Status,
                LastHeartbeatUtc = view.LastHeartbeatUtc,
                MaxTemperatureThreshold = view.MaxTemperatureThreshold
            };
        }

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
