using System.Data;
using System.Text.Json;
using Application.Abstractions;
using Domain.Abstractions;
using Domain.Events;
using Infrastructure.Data;
using Infrastructure.Data.Entities;
using Microsoft.EntityFrameworkCore;

namespace Infrastructure.EventSourcing;

public sealed class PostgresEventStore(InfrastructureDbContext dbContext) : IEventStore
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    private readonly InfrastructureDbContext _dbContext = dbContext;

    public async Task<IReadOnlyList<IDomainEvent>> LoadAsync(Guid streamId, CancellationToken cancellationToken)
    {
        var records = await _dbContext.DeviceEvents
            .AsNoTracking()
            .Where(x => x.StreamId == streamId)
            .OrderBy(x => x.Version)
            .Select(x => new { x.EventType, x.Payload })
            .ToListAsync(cancellationToken);

        return records.Select(x => Deserialize(x.EventType, x.Payload)).ToArray();
    }

    public async Task<IReadOnlyList<StoredEvent>> LoadAllAsync(CancellationToken cancellationToken)
    {
        var records = await _dbContext.DeviceEvents
            .AsNoTracking()
            .OrderBy(x => x.StreamId)
            .ThenBy(x => x.Version)
            .Select(x => new
            {
                x.StreamId,
                x.Version,
                x.EventType,
                x.Payload,
                x.CorrelationId,
                x.CausationId
            })
            .ToListAsync(cancellationToken);

        return records
            .Select(x => new StoredEvent(
                x.StreamId,
                x.Version,
                Deserialize(x.EventType, x.Payload),
                new CommandMetadata(x.CorrelationId, x.CausationId)))
            .ToArray();
    }

    public async Task AppendAsync(
        Guid streamId,
        int expectedVersion,
        IReadOnlyCollection<IDomainEvent> events,
        CommandMetadata? commandMetadata,
        CancellationToken cancellationToken)
    {
        if (events.Count == 0)
        {
            return;
        }

        await using var transaction = await _dbContext.Database.BeginTransactionAsync(IsolationLevel.Serializable, cancellationToken);

        try
        {
            var currentVersion = await _dbContext.DeviceEvents
                .Where(x => x.StreamId == streamId)
                .Select(x => (int?)x.Version)
                .MaxAsync(cancellationToken) ?? -1;

            if (currentVersion != expectedVersion)
            {
                throw new EventStoreConcurrencyException(streamId, expectedVersion, currentVersion);
            }

            var nextVersion = expectedVersion + 1;
            var correlationId = commandMetadata?.CorrelationId ?? Guid.Empty;
            var causationId = commandMetadata?.CausationId;
            foreach (var domainEvent in events)
            {
                _dbContext.DeviceEvents.Add(new EventRecord
                {
                    StreamId = streamId,
                    Version = nextVersion,
                    EventType = domainEvent.GetType().Name,
                    Payload = JsonSerializer.Serialize(domainEvent, domainEvent.GetType(), JsonOptions),
                    OccurredOnUtc = domainEvent.OccurredOnUtc,
                    CorrelationId = correlationId,
                    CausationId = causationId
                });
                nextVersion++;
            }

            await _dbContext.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
        }
        catch (DbUpdateException ex)
        {
            await transaction.RollbackAsync(cancellationToken);
            var actualVersion = await GetCurrentVersionAsync(streamId, cancellationToken);
            throw new EventStoreConcurrencyException(streamId, expectedVersion, actualVersion, ex);
        }
        catch
        {
            await transaction.RollbackAsync(cancellationToken);
            throw;
        }
    }

    private static IDomainEvent Deserialize(string eventType, string payload)
    {
        return eventType switch
        {
            nameof(DeviceRegistered) => Deserialize<DeviceRegistered>(payload),
            nameof(DeviceStarted) => Deserialize<DeviceStarted>(payload),
            nameof(DeviceStopped) => Deserialize<DeviceStopped>(payload),
            nameof(DeviceMaintenanceModeChanged) => Deserialize<DeviceMaintenanceModeChanged>(payload),
            _ => throw new NotSupportedException($"Unsupported domain event type '{eventType}'.")
        };
    }

    private async Task<int?> GetCurrentVersionAsync(Guid streamId, CancellationToken cancellationToken)
    {
        return await _dbContext.DeviceEvents
            .Where(x => x.StreamId == streamId)
            .Select(x => (int?)x.Version)
            .MaxAsync(cancellationToken);
    }

    private static T Deserialize<T>(string payload) where T : IDomainEvent
    {
        return JsonSerializer.Deserialize<T>(payload, JsonOptions)
            ?? throw new InvalidOperationException($"Unable to deserialize event payload for '{typeof(T).Name}'.");
    }
}
