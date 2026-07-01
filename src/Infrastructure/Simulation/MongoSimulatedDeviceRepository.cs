using Application.Simulation.Abstractions;
using Application.Simulation.Models;
using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;
using MongoDB.Driver;

namespace Infrastructure.Simulation;

public sealed class MongoSimulatedDeviceRepository : ISimulatedDeviceRepository
{
    private readonly IMongoCollection<SimulatedDeviceDocument> _collection;
    private readonly SemaphoreSlim _initializationLock = new(1, 1);
    private volatile bool _initialized;

    public MongoSimulatedDeviceRepository(IMongoClient mongoClient, string databaseName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(databaseName);

        _collection = mongoClient
            .GetDatabase(databaseName)
            .GetCollection<SimulatedDeviceDocument>("simulated_device_views");
    }

    public async Task<IReadOnlyList<SimulatedDeviceView>> GetAllAsync(CancellationToken cancellationToken)
    {
        await EnsureIndexesAsync(cancellationToken);

        var documents = await _collection
            .Find(FilterDefinition<SimulatedDeviceDocument>.Empty)
            .SortBy(x => x.DeviceCode)
            .ToListAsync(cancellationToken);

        return documents.Select(x => x.ToView()).ToArray();
    }

    public async Task<IReadOnlyList<Guid>> GetAllDeviceIdsAsync(CancellationToken cancellationToken)
    {
        await EnsureIndexesAsync(cancellationToken);

        var ids = await _collection
            .Find(FilterDefinition<SimulatedDeviceDocument>.Empty)
            .Project(x => x.DeviceId)
            .ToListAsync(cancellationToken);

        return ids;
    }

    public async Task<IReadOnlyList<SimulatedDeviceView>> GetByIdsAsync(
        IReadOnlyCollection<Guid> deviceIds,
        CancellationToken cancellationToken)
    {
        await EnsureIndexesAsync(cancellationToken);

        if (deviceIds.Count == 0)
        {
            return Array.Empty<SimulatedDeviceView>();
        }

        var documents = await _collection
            .Find(x => deviceIds.Contains(x.DeviceId))
            .ToListAsync(cancellationToken);

        return documents.Select(x => x.ToView()).ToArray();
    }

    public async Task<SimulatedDeviceView?> GetAsync(Guid deviceId, CancellationToken cancellationToken)
    {
        await EnsureIndexesAsync(cancellationToken);

        var document = await _collection
            .Find(x => x.DeviceId == deviceId)
            .FirstOrDefaultAsync(cancellationToken);

        return document?.ToView();
    }

    public async Task UpsertAsync(SimulatedDeviceView view, CancellationToken cancellationToken)
    {
        await EnsureIndexesAsync(cancellationToken);

        var document = SimulatedDeviceDocument.FromView(view);
        await _collection.ReplaceOneAsync(
            x => x.DeviceId == view.DeviceId,
            document,
            new ReplaceOptions { IsUpsert = true },
            cancellationToken);
    }

    public async Task ResetAsync(CancellationToken cancellationToken)
    {
        await EnsureIndexesAsync(cancellationToken);
        await _collection.DeleteManyAsync(FilterDefinition<SimulatedDeviceDocument>.Empty, cancellationToken);
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

            var deviceCodeIndex = new CreateIndexModel<SimulatedDeviceDocument>(
                Builders<SimulatedDeviceDocument>.IndexKeys.Ascending(x => x.DeviceCode),
                new CreateIndexOptions { Name = "ix_simulated_device_views_device_code", Unique = true });

            await _collection.Indexes.CreateOneAsync(deviceCodeIndex, cancellationToken: cancellationToken);
            _initialized = true;
        }
        finally
        {
            _initializationLock.Release();
        }
    }

    private sealed class SimulatedDeviceDocument
    {
        [BsonId]
        [BsonGuidRepresentation(GuidRepresentation.Standard)]
        public Guid DeviceId { get; init; }

        public string DeviceCode { get; init; } = string.Empty;

        public string Status { get; init; } = string.Empty;

        public double CurrentTemperature { get; init; }

        public double MaxTemperatureThreshold { get; init; }

        public DateTime LastUpdatedUtc { get; init; }

        public long Sequence { get; init; }

        public static SimulatedDeviceDocument FromView(SimulatedDeviceView view)
        {
            return new SimulatedDeviceDocument
            {
                DeviceId = view.DeviceId,
                DeviceCode = view.DeviceCode,
                Status = view.Status,
                CurrentTemperature = view.CurrentTemperature,
                MaxTemperatureThreshold = view.MaxTemperatureThreshold,
                LastUpdatedUtc = view.LastUpdatedUtc,
                Sequence = view.Sequence
            };
        }

        public SimulatedDeviceView ToView()
        {
            return new SimulatedDeviceView(
                DeviceId,
                DeviceCode,
                Status,
                CurrentTemperature,
                MaxTemperatureThreshold,
                LastUpdatedUtc,
                Sequence);
        }
    }
}
