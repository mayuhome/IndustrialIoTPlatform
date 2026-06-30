using Application.Abstractions;
using Application.Devices.Commands;
using Application.Devices.Queries;
using MongoDB.Bson;
using MongoDB.Driver;
using Npgsql;
using System.Net.Sockets;
using StackExchange.Redis;
using Infrastructure.EventSourcing;
using Infrastructure.ReadModels;

var builder = WebApplication.CreateBuilder(args);

ValidateInfrastructureConfiguration(builder.Configuration);

// 1. Add services to the container.
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSingleton<IMongoClient>(_ =>
    new MongoClient(builder.Configuration.GetConnectionString("Mongo")));
builder.Services.AddSingleton<IConnectionMultiplexer>(_ =>
    ConnectionMultiplexer.Connect(builder.Configuration.GetConnectionString("Redis")!));
builder.Services.AddSingleton<IEventStore>(_ =>
    new PostgresEventStore(
        builder.Configuration.GetConnectionString("Postgres")!,
        builder.Configuration["EventStore:Schema"]!));
builder.Services.AddSingleton<IDeviceStatusReadRepository>(sp =>
    new MongoCachedDeviceStatusReadRepository(
        sp.GetRequiredService<IMongoClient>(),
        builder.Configuration["Projection:MongoDatabase"]!,
        sp.GetRequiredService<IConnectionMultiplexer>(),
        builder.Configuration.GetValue<int?>("Cache:DefaultTtlSeconds") ?? 300));
builder.Services.AddTransient<RegisterDeviceCommandHandler>();
builder.Services.AddTransient<StartDeviceCommandHandler>();
builder.Services.AddTransient<GetDeviceStatusQueryHandler>();

// 2. Add Swagger/OpenAPI support
builder.Services.AddSwaggerGen(options =>
{
    options.SwaggerDoc("v1", new()
    {
        Version = "v1",
        Title = "Industrial IoT Platform API",
        Description = "An ASP.NET Core Web API for Industrial IoT Platform",
    });
});

var app = builder.Build();

await ValidateInfrastructureConnectivityAsync(app.Services, app.Configuration, app.Logger, app.Lifetime.ApplicationStopping);

// 3. Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI(options =>
    {
        options.SwaggerEndpoint("/swagger/v1/swagger.json", "Industrial IoT Platform API v1");
        options.RoutePrefix = string.Empty; // Set Swagger UI at the app's root
    });
}

app.UseHttpsRedirection();
app.UseAuthorization();

app.MapPost(
    "/devices/register",
    async (RegisterDeviceRequest request, RegisterDeviceCommandHandler handler, CancellationToken ct) =>
    {
        var command = new RegisterDeviceCommand(request.DeviceCode, request.MaxTemperatureThreshold);
        var deviceId = await handler.Handle(command, ct);
        return Results.Created($"/devices/{deviceId}/status", new RegisterDeviceResponse(deviceId));
    });

app.MapPost(
    "/devices/{deviceId:guid}/start",
    async (Guid deviceId, StartDeviceRequest request, StartDeviceCommandHandler handler, CancellationToken ct) =>
    {
        var command = new StartDeviceCommand(deviceId, request.CurrentTemperature);
        await handler.Handle(command, ct);
        return Results.Accepted($"/devices/{deviceId}/status");
    });

app.MapGet(
    "/devices/{deviceId:guid}/status",
    async (Guid deviceId, GetDeviceStatusQueryHandler handler, CancellationToken ct) =>
    {
        var query = new GetDeviceStatusQuery(deviceId);
        var result = await handler.Handle(query, ct);
        return result is null ? Results.NotFound() : Results.Ok(result);
    });

// 4. map to router
app.MapControllers();
app.Run();

static void ValidateInfrastructureConfiguration(IConfiguration configuration)
{
    var postgres = configuration.GetConnectionString("Postgres");
    var redis = configuration.GetConnectionString("Redis");
    var mongo = configuration.GetConnectionString("Mongo");
    var eventStoreSchema = configuration["EventStore:Schema"];
    var projectionDatabase = configuration["Projection:MongoDatabase"];

    var missing = new List<string>();

    if (string.IsNullOrWhiteSpace(postgres))
    {
        missing.Add("ConnectionStrings:Postgres");
    }

    if (string.IsNullOrWhiteSpace(redis))
    {
        missing.Add("ConnectionStrings:Redis");
    }

    if (string.IsNullOrWhiteSpace(mongo))
    {
        missing.Add("ConnectionStrings:Mongo");
    }
    else
    {
        try
        {
            _ = MongoUrl.Create(mongo);
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException(
                "ConnectionStrings:Mongo is invalid. If the username or password contains reserved URI characters such as @, :, /, ?, #, or %, URL-encode them first.",
                ex);
        }
    }

    if (string.IsNullOrWhiteSpace(eventStoreSchema))
    {
        missing.Add("EventStore:Schema");
    }

    if (string.IsNullOrWhiteSpace(projectionDatabase))
    {
        missing.Add("Projection:MongoDatabase");
    }

    if (missing.Count > 0)
    {
        throw new InvalidOperationException(
            "Missing required infrastructure settings: " + string.Join(", ", missing));
    }
}

static async Task ValidateInfrastructureConnectivityAsync(
    IServiceProvider services,
    IConfiguration configuration,
    ILogger logger,
    CancellationToken cancellationToken)
{
    await ValidatePostgresConnectivityAsync(configuration, logger, cancellationToken);
    await ValidateRedisConnectivityAsync(services, logger);
    await ValidateMongoConnectivityAsync(services, configuration, logger, cancellationToken);
}

static async Task ValidatePostgresConnectivityAsync(
    IConfiguration configuration,
    ILogger logger,
    CancellationToken cancellationToken)
{
    var connectionString = configuration.GetConnectionString("Postgres")!;

    try
    {
        await using var connection = new NpgsqlConnection(connectionString);
        await connection.OpenAsync(cancellationToken);

        await using var command = new NpgsqlCommand("SELECT 1;", connection);
        await command.ExecuteScalarAsync(cancellationToken);

        logger.LogInformation("PostgreSQL connectivity probe succeeded.");
    }
    catch (PostgresException ex) when (ex.SqlState == PostgresErrorCodes.InvalidCatalogName)
    {
        throw new InvalidOperationException(
            "PostgreSQL connectivity probe failed: target database does not exist. " +
            "Check ConnectionStrings:Postgres -> Database. If you are using Docker Compose, recreate the volume or create the database manually.",
            ex);
    }
    catch (PostgresException ex) when (ex.SqlState == PostgresErrorCodes.InvalidPassword)
    {
        throw new InvalidOperationException(
            "PostgreSQL connectivity probe failed: username or password is invalid.",
            ex);
    }
    catch (PostgresException ex) when (ex.SqlState == PostgresErrorCodes.InvalidAuthorizationSpecification)
    {
        throw new InvalidOperationException(
            "PostgreSQL connectivity probe failed: authorization configuration is invalid. Check username and authentication settings.",
            ex);
    }
    catch (NpgsqlException ex) when (ex.InnerException is SocketException)
    {
        throw new InvalidOperationException(
            "PostgreSQL connectivity probe failed: host or port is unreachable. Check ConnectionStrings:Postgres -> Host and Port.",
            ex);
    }
}

static async Task ValidateRedisConnectivityAsync(IServiceProvider services, ILogger logger)
{
    try
    {
        var redis = services.GetRequiredService<IConnectionMultiplexer>();
        var database = redis.GetDatabase();
        _ = await database.PingAsync();

        logger.LogInformation("Redis connectivity probe succeeded.");
    }
    catch (RedisServerException ex) when (ex.Message.Contains("NOAUTH", StringComparison.OrdinalIgnoreCase) || ex.Message.Contains("WRONGPASS", StringComparison.OrdinalIgnoreCase))
    {
        throw new InvalidOperationException(
            "Redis connectivity probe failed: authentication failed. Check ConnectionStrings:Redis password configuration.",
            ex);
    }
    catch (RedisConnectionException ex) when (ex.Message.Contains("AuthenticationFailure", StringComparison.OrdinalIgnoreCase) || ex.Message.Contains("NOAUTH", StringComparison.OrdinalIgnoreCase) || ex.Message.Contains("WRONGPASS", StringComparison.OrdinalIgnoreCase))
    {
        throw new InvalidOperationException(
            "Redis connectivity probe failed: authentication failed. Check ConnectionStrings:Redis password configuration.",
            ex);
    }
    catch (RedisConnectionException ex)
    {
        throw new InvalidOperationException(
            "Redis connectivity probe failed: host or port is unreachable. Check ConnectionStrings:Redis endpoint configuration.",
            ex);
    }
}

static async Task ValidateMongoConnectivityAsync(
    IServiceProvider services,
    IConfiguration configuration,
    ILogger logger,
    CancellationToken cancellationToken)
{
    var databaseName = configuration["Projection:MongoDatabase"]!;

    try
    {
        var mongoClient = services.GetRequiredService<IMongoClient>();
        var database = mongoClient.GetDatabase(databaseName);
        await database.RunCommandAsync((Command<BsonDocument>)"{ ping: 1 }", cancellationToken: cancellationToken);

        logger.LogInformation("MongoDB connectivity probe succeeded.");
    }
    catch (MongoAuthenticationException ex)
    {
        throw new InvalidOperationException(
            "MongoDB connectivity probe failed: authentication failed. Check ConnectionStrings:Mongo username, password, and authSource.",
            ex);
    }
    catch (TimeoutException ex)
    {
        throw new InvalidOperationException(
            "MongoDB connectivity probe failed: server did not respond in time. Check host, port, and network reachability.",
            ex);
    }
    catch (MongoConnectionException ex)
    {
        throw new InvalidOperationException(
            "MongoDB connectivity probe failed: host or port is unreachable. Check ConnectionStrings:Mongo endpoint configuration.",
            ex);
    }
}

public sealed record RegisterDeviceRequest(string DeviceCode, double MaxTemperatureThreshold);

public sealed record RegisterDeviceResponse(Guid DeviceId);

public sealed record StartDeviceRequest(double CurrentTemperature);
