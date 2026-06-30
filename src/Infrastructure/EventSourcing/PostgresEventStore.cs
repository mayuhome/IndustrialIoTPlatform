using System.Data;
using System.Text.Json;
using Application.Abstractions;
using Domain.Abstractions;
using Domain.Events;
using Npgsql;

namespace Infrastructure.EventSourcing;

public sealed class PostgresEventStore : IEventStore
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private readonly string _connectionString;
    private readonly string _schema;
    private readonly SemaphoreSlim _initializationLock = new(1, 1);
    private volatile bool _initialized;

    public PostgresEventStore(string connectionString, string schema)
    {
        _connectionString = string.IsNullOrWhiteSpace(connectionString)
            ? throw new ArgumentException("Postgres connection string is required.", nameof(connectionString))
            : connectionString;
        _schema = ValidateSchemaName(schema);
    }

    public async Task<IReadOnlyList<IDomainEvent>> LoadAsync(Guid streamId, CancellationToken cancellationToken)
    {
        await EnsureCreatedAsync(cancellationToken);

        await using var connection = new NpgsqlConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);

        var sql = $"""
            SELECT event_type, payload
            FROM {_schema}.device_events
            WHERE stream_id = @streamId
            ORDER BY version ASC;
            """;

        await using var command = new NpgsqlCommand(sql, connection);
        command.Parameters.AddWithValue("streamId", streamId);

        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        var events = new List<IDomainEvent>();

        while (await reader.ReadAsync(cancellationToken))
        {
            var eventType = reader.GetString(0);
            var payload = reader.GetString(1);
            events.Add(Deserialize(eventType, payload));
        }

        return events;
    }

    public async Task AppendAsync(
        Guid streamId,
        int expectedVersion,
        IReadOnlyCollection<IDomainEvent> events,
        CancellationToken cancellationToken)
    {
        if (events.Count == 0)
        {
            return;
        }

        await EnsureCreatedAsync(cancellationToken);

        await using var connection = new NpgsqlConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(IsolationLevel.Serializable, cancellationToken);

        try
        {
            var currentVersion = await GetCurrentVersionAsync(connection, transaction, streamId, cancellationToken);
            if (currentVersion != expectedVersion)
            {
                throw new InvalidOperationException(
                    $"Concurrency conflict for stream '{streamId}'. Expected version {expectedVersion}, actual version {currentVersion}.");
            }

            var nextVersion = expectedVersion + 1;
            foreach (var domainEvent in events)
            {
                await InsertEventAsync(connection, transaction, streamId, nextVersion, domainEvent, cancellationToken);
                nextVersion++;
            }

            await transaction.CommitAsync(cancellationToken);
        }
        catch (PostgresException ex) when (ex.SqlState is PostgresErrorCodes.SerializationFailure or PostgresErrorCodes.UniqueViolation)
        {
            await transaction.RollbackAsync(cancellationToken);
            throw new InvalidOperationException(
                $"Concurrency conflict for stream '{streamId}'. Transaction could not be committed.", ex);
        }
        catch
        {
            await transaction.RollbackAsync(cancellationToken);
            throw;
        }
    }

    private async Task EnsureCreatedAsync(CancellationToken cancellationToken)
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

            await using var connection = new NpgsqlConnection(_connectionString);
            await connection.OpenAsync(cancellationToken);

            var sql = $"""
                CREATE SCHEMA IF NOT EXISTS {_schema};

                CREATE TABLE IF NOT EXISTS {_schema}.device_events (
                    stream_id uuid NOT NULL,
                    version integer NOT NULL,
                    event_type text NOT NULL,
                    payload jsonb NOT NULL,
                    occurred_on_utc timestamptz NOT NULL,
                    PRIMARY KEY (stream_id, version)
                );

                CREATE INDEX IF NOT EXISTS ix_device_events_occurred_on_utc
                    ON {_schema}.device_events (occurred_on_utc);
                """;

            await using var command = new NpgsqlCommand(sql, connection);
            await command.ExecuteNonQueryAsync(cancellationToken);
            _initialized = true;
        }
        finally
        {
            _initializationLock.Release();
        }
    }

    private async Task<int> GetCurrentVersionAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        Guid streamId,
        CancellationToken cancellationToken)
    {
        var sql = $"SELECT COALESCE(MAX(version), -1) FROM {_schema}.device_events WHERE stream_id = @streamId;";
        await using var command = new NpgsqlCommand(sql, connection, transaction);
        command.Parameters.AddWithValue("streamId", streamId);

        var result = await command.ExecuteScalarAsync(cancellationToken);
        return Convert.ToInt32(result);
    }

    private async Task InsertEventAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        Guid streamId,
        int version,
        IDomainEvent domainEvent,
        CancellationToken cancellationToken)
    {
        var sql = $"""
            INSERT INTO {_schema}.device_events (stream_id, version, event_type, payload, occurred_on_utc)
            VALUES (@streamId, @version, @eventType, @payload::jsonb, @occurredOnUtc);
            """;

        await using var command = new NpgsqlCommand(sql, connection, transaction);
        command.Parameters.AddWithValue("streamId", streamId);
        command.Parameters.AddWithValue("version", version);
        command.Parameters.AddWithValue("eventType", domainEvent.GetType().Name);
        command.Parameters.AddWithValue("payload", JsonSerializer.Serialize(domainEvent, domainEvent.GetType(), JsonOptions));
        command.Parameters.AddWithValue("occurredOnUtc", domainEvent.OccurredOnUtc);

        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    private static IDomainEvent Deserialize(string eventType, string payload)
    {
        return eventType switch
        {
            nameof(DeviceRegistered) => Deserialize<DeviceRegistered>(payload),
            nameof(DeviceStarted) => Deserialize<DeviceStarted>(payload),
            _ => throw new NotSupportedException($"Unsupported domain event type '{eventType}'.")
        };
    }

    private static T Deserialize<T>(string payload) where T : IDomainEvent
    {
        return JsonSerializer.Deserialize<T>(payload, JsonOptions)
            ?? throw new InvalidOperationException($"Unable to deserialize event payload for '{typeof(T).Name}'.");
    }

    private static string ValidateSchemaName(string schema)
    {
        if (string.IsNullOrWhiteSpace(schema))
        {
            throw new ArgumentException("Event store schema is required.", nameof(schema));
        }

        foreach (var ch in schema)
        {
            if (!(char.IsLetterOrDigit(ch) || ch == '_'))
            {
                throw new ArgumentException("Event store schema contains invalid characters.", nameof(schema));
            }
        }

        return schema;
    }
}
