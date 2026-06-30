using System.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Npgsql;

namespace Infrastructure.Data;

public sealed class InfrastructureDatabaseInitializer(
    InfrastructureDbContext dbContext,
    IConfiguration configuration)
{
    private readonly InfrastructureDbContext _dbContext = dbContext;
    private readonly string _eventStoreSchema = configuration["EventStore:Schema"] ?? "public";
    private readonly NpgsqlCommandBuilder _commandBuilder = new();

    public async Task InitializeAsync(CancellationToken cancellationToken)
    {
        await using var connection = (NpgsqlConnection)_dbContext.Database.GetDbConnection();
        if (connection.State != ConnectionState.Open)
        {
            await connection.OpenAsync(cancellationToken);
        }

        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);

        try
        {
            await EnsureSchemaAsync(connection, transaction, _eventStoreSchema, cancellationToken);
            await EnsureDeviceEventsTableAsync(connection, transaction, cancellationToken);
            await EnsureUsersTableAsync(connection, transaction, cancellationToken);
            await transaction.CommitAsync(cancellationToken);
        }
        catch
        {
            await transaction.RollbackAsync(cancellationToken);
            throw;
        }
    }

    private async Task EnsureDeviceEventsTableAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        CancellationToken cancellationToken)
    {
        const string columnsDefinition = @"
            stream_id uuid NOT NULL,
            version integer NOT NULL,
            event_type text NOT NULL,
            payload jsonb NOT NULL,
            occurred_on_utc timestamp with time zone NOT NULL,
            correlation_id uuid NOT NULL,
            causation_id uuid NULL";

        await EnsureTableAsync(
            connection,
            transaction,
            _eventStoreSchema,
            "device_events",
            columnsDefinition,
            cancellationToken);

        await RenameColumnIfNeededAsync(connection, transaction, _eventStoreSchema, "device_events", "StreamId", "stream_id", cancellationToken);
        await RenameColumnIfNeededAsync(connection, transaction, _eventStoreSchema, "device_events", "Version", "version", cancellationToken);
        await RenameColumnIfNeededAsync(connection, transaction, _eventStoreSchema, "device_events", "EventType", "event_type", cancellationToken);
        await RenameColumnIfNeededAsync(connection, transaction, _eventStoreSchema, "device_events", "Payload", "payload", cancellationToken);
        await RenameColumnIfNeededAsync(connection, transaction, _eventStoreSchema, "device_events", "OccurredOnUtc", "occurred_on_utc", cancellationToken);
        await RenameColumnIfNeededAsync(connection, transaction, _eventStoreSchema, "device_events", "CorrelationId", "correlation_id", cancellationToken);
        await RenameColumnIfNeededAsync(connection, transaction, _eventStoreSchema, "device_events", "CausationId", "causation_id", cancellationToken);

        await EnsureColumnAsync(connection, transaction, _eventStoreSchema, "device_events", "stream_id", "uuid", cancellationToken);
        await EnsureColumnAsync(connection, transaction, _eventStoreSchema, "device_events", "version", "integer", cancellationToken);
        await EnsureColumnAsync(connection, transaction, _eventStoreSchema, "device_events", "event_type", "text", cancellationToken);
        await EnsureColumnAsync(connection, transaction, _eventStoreSchema, "device_events", "payload", "jsonb", cancellationToken);
        await EnsureColumnAsync(connection, transaction, _eventStoreSchema, "device_events", "occurred_on_utc", "timestamp with time zone", cancellationToken);
        await EnsureColumnAsync(connection, transaction, _eventStoreSchema, "device_events", "correlation_id", "uuid NOT NULL DEFAULT '00000000-0000-0000-0000-000000000000'", cancellationToken);
        await EnsureColumnAsync(connection, transaction, _eventStoreSchema, "device_events", "causation_id", "uuid", cancellationToken);

        await EnsurePrimaryKeyAsync(
            connection,
            transaction,
            _eventStoreSchema,
            "device_events",
            "pk_device_events",
            "stream_id, version",
            cancellationToken);

        var deviceEventsTable = QuoteQualifiedName(_eventStoreSchema, "device_events");
        await EnsureIndexAsync(
            connection,
            transaction,
            _eventStoreSchema,
            "ix_device_events_occurred_on_utc",
            $"CREATE INDEX IF NOT EXISTS ix_device_events_occurred_on_utc ON {deviceEventsTable} (occurred_on_utc)",
            cancellationToken);
    }

    private async Task EnsureUsersTableAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        CancellationToken cancellationToken)
    {
        const string columnsDefinition = @"
            id uuid NOT NULL,
            username text NOT NULL,
            normalized_username text NOT NULL,
            password_hash text NOT NULL,
            role text NOT NULL,
            created_at_utc timestamp with time zone NOT NULL";

        await EnsureTableAsync(
            connection,
            transaction,
            "public",
            "users",
            columnsDefinition,
            cancellationToken);

        await RenameColumnIfNeededAsync(connection, transaction, "public", "users", "Id", "id", cancellationToken);
        await RenameColumnIfNeededAsync(connection, transaction, "public", "users", "Username", "username", cancellationToken);
        await RenameColumnIfNeededAsync(connection, transaction, "public", "users", "NormalizedUsername", "normalized_username", cancellationToken);
        await RenameColumnIfNeededAsync(connection, transaction, "public", "users", "PasswordHash", "password_hash", cancellationToken);
        await RenameColumnIfNeededAsync(connection, transaction, "public", "users", "Role", "role", cancellationToken);
        await RenameColumnIfNeededAsync(connection, transaction, "public", "users", "CreatedAtUtc", "created_at_utc", cancellationToken);

        await EnsureColumnAsync(connection, transaction, "public", "users", "id", "uuid", cancellationToken);
        await EnsureColumnAsync(connection, transaction, "public", "users", "username", "text", cancellationToken);
        await EnsureColumnAsync(connection, transaction, "public", "users", "normalized_username", "text", cancellationToken);
        await EnsureColumnAsync(connection, transaction, "public", "users", "password_hash", "text", cancellationToken);
        await EnsureColumnAsync(connection, transaction, "public", "users", "role", "text", cancellationToken);
        await EnsureColumnAsync(connection, transaction, "public", "users", "created_at_utc", "timestamp with time zone", cancellationToken);

        await EnsurePrimaryKeyAsync(
            connection,
            transaction,
            "public",
            "users",
            "pk_users",
            "id",
            cancellationToken);

        await EnsureIndexAsync(
            connection,
            transaction,
            "public",
            "ix_users_normalized_username",
            "CREATE UNIQUE INDEX IF NOT EXISTS ix_users_normalized_username ON public.users (normalized_username)",
            cancellationToken);
    }

    private async Task EnsureSchemaAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        string schema,
        CancellationToken cancellationToken)
    {
        await ExecuteNonQueryAsync(
            connection,
            transaction,
            $"CREATE SCHEMA IF NOT EXISTS {QuoteIdentifier(schema)}",
            cancellationToken);
    }

    private async Task EnsureTableAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        string schema,
        string table,
        string columnsDefinition,
        CancellationToken cancellationToken)
    {
        await ExecuteNonQueryAsync(
            connection,
            transaction,
            $"CREATE TABLE IF NOT EXISTS {QuoteQualifiedName(schema, table)} ({columnsDefinition})",
            cancellationToken);
    }

    private async Task RenameColumnIfNeededAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        string schema,
        string table,
        string oldColumn,
        string newColumn,
        CancellationToken cancellationToken)
    {
        if (await ColumnExistsAsync(connection, transaction, schema, table, newColumn, cancellationToken))
        {
            return;
        }

        if (!await ColumnExistsAsync(connection, transaction, schema, table, oldColumn, cancellationToken))
        {
            return;
        }

        await ExecuteNonQueryAsync(
            connection,
            transaction,
            $"ALTER TABLE {QuoteQualifiedName(schema, table)} RENAME COLUMN {QuoteIdentifier(oldColumn)} TO {QuoteIdentifier(newColumn)}",
            cancellationToken);
    }

    private async Task EnsureColumnAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        string schema,
        string table,
        string column,
        string dataType,
        CancellationToken cancellationToken)
    {
        if (await ColumnExistsAsync(connection, transaction, schema, table, column, cancellationToken))
        {
            return;
        }

        await ExecuteNonQueryAsync(
            connection,
            transaction,
            $"ALTER TABLE {QuoteQualifiedName(schema, table)} ADD COLUMN {QuoteIdentifier(column)} {dataType}",
            cancellationToken);
    }

    private async Task EnsurePrimaryKeyAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        string schema,
        string table,
        string constraintName,
        string columnList,
        CancellationToken cancellationToken)
    {
        const string sql = """
            SELECT EXISTS (
                SELECT 1
                FROM pg_constraint c
                JOIN pg_class t ON t.oid = c.conrelid
                JOIN pg_namespace n ON n.oid = t.relnamespace
                WHERE c.contype = 'p'
                  AND n.nspname = @schema
                  AND t.relname = @table
            )
            """;

        if (await ExecuteScalarAsync<bool>(connection, transaction, sql, cancellationToken, ("schema", schema), ("table", table)))
        {
            return;
        }

        await ExecuteNonQueryAsync(
            connection,
            transaction,
            $"ALTER TABLE {QuoteQualifiedName(schema, table)} ADD CONSTRAINT {QuoteIdentifier(constraintName)} PRIMARY KEY ({columnList})",
            cancellationToken);
    }

    private async Task EnsureIndexAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        string schema,
        string indexName,
        string createIndexSql,
        CancellationToken cancellationToken)
    {
        const string sql = "SELECT to_regclass(@qualifiedIndexName) IS NOT NULL";
        var qualifiedIndexName = $"{schema}.{indexName}";

        if (await ExecuteScalarAsync<bool>(connection, transaction, sql, cancellationToken, ("qualifiedIndexName", qualifiedIndexName)))
        {
            return;
        }

        await ExecuteNonQueryAsync(connection, transaction, createIndexSql, cancellationToken);
    }

    private async Task<bool> ColumnExistsAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        string schema,
        string table,
        string column,
        CancellationToken cancellationToken)
    {
        const string sql = """
            SELECT EXISTS (
                SELECT 1
                FROM information_schema.columns
                WHERE table_schema = @schema
                  AND table_name = @table
                  AND column_name = @column
            )
            """;

        return await ExecuteScalarAsync<bool>(
            connection,
            transaction,
            sql,
            cancellationToken,
            ("schema", schema),
            ("table", table),
            ("column", column));
    }

    private async Task ExecuteNonQueryAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        string sql,
        CancellationToken cancellationToken,
        params (string Name, object? Value)[] parameters)
    {
        await using var command = new NpgsqlCommand(sql, connection, transaction);
        foreach (var (name, value) in parameters)
        {
            command.Parameters.AddWithValue(name, value ?? DBNull.Value);
        }

        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    private async Task<T> ExecuteScalarAsync<T>(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        string sql,
        CancellationToken cancellationToken,
        params (string Name, object? Value)[] parameters)
    {
        await using var command = new NpgsqlCommand(sql, connection, transaction);
        foreach (var (name, value) in parameters)
        {
            command.Parameters.AddWithValue(name, value ?? DBNull.Value);
        }

        var result = await command.ExecuteScalarAsync(cancellationToken);
        return result is T typedResult
            ? typedResult
            : throw new InvalidOperationException($"Unexpected scalar result for SQL: {sql}");
    }

    private string QuoteIdentifier(string identifier)
    {
        return _commandBuilder.QuoteIdentifier(identifier);
    }

    private string QuoteQualifiedName(string schema, string name)
    {
        return $"{QuoteIdentifier(schema)}.{QuoteIdentifier(name)}";
    }
}
