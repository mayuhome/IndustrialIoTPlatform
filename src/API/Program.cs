using Application.Abstractions;
using Application.Devices.Commands;
using Application.Devices.Queries;
using MongoDB.Driver;
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

public sealed record RegisterDeviceRequest(string DeviceCode, double MaxTemperatureThreshold);

public sealed record RegisterDeviceResponse(Guid DeviceId);

public sealed record StartDeviceRequest(double CurrentTemperature);
