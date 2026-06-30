using Application.Abstractions;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using MongoDB.Bson;
using MongoDB.Driver;
using Npgsql;
using System.Text;
using System.Net.Sockets;
using StackExchange.Redis;
using Infrastructure.Data;
using Infrastructure.EventSourcing;
using Infrastructure.ReadModels;
using Infrastructure.Security;
using Infrastructure.Users;
using Application.Auth.Commands;
using Application.Auth.Queries;
using Application.Devices.Commands;
using Application.Devices.Projections;
using Application.Devices.Queries;
using Application.Simulation;
using Application.Simulation.Abstractions;
using Application.Simulation.Commands;
using Application.Simulation.Queries;
using Infrastructure.Simulation;

var builder = WebApplication.CreateBuilder(args);

ValidateInfrastructureConfiguration(builder.Configuration);
builder.WebHost.UseUrls($"http://0.0.0.0:{builder.Configuration.GetValue<int?>("Server:HttpPort") ?? 5000}");

// 1. Add services to the container.
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddAuthorization();
builder.Services
    .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateIssuerSigningKey = true,
            ValidateLifetime = true,
            ValidIssuer = builder.Configuration["Jwt:Issuer"],
            ValidAudience = builder.Configuration["Jwt:Audience"],
            IssuerSigningKey = new SymmetricSecurityKey(
                Encoding.UTF8.GetBytes(builder.Configuration["Jwt:SigningKey"]!)),
            ClockSkew = TimeSpan.FromMinutes(1)
        };
    });
builder.Services.AddDbContext<InfrastructureDbContext>(options =>
    options.UseNpgsql(builder.Configuration.GetConnectionString("Postgres")));
builder.Services.AddScoped<InfrastructureDatabaseInitializer>();
builder.Services.AddSingleton<IMongoClient>(_ =>
    new MongoClient(builder.Configuration.GetConnectionString("Mongo")));
builder.Services.AddSingleton<IConnectionMultiplexer>(_ =>
    ConnectionMultiplexer.Connect(builder.Configuration.GetConnectionString("Redis")!));
builder.Services.AddScoped<IUserRepository, PostgresUserRepository>();
builder.Services.AddSingleton<IPasswordHasher, Pbkdf2PasswordHasher>();
builder.Services.AddSingleton<ITokenIssuer>(_ =>
    new JwtTokenIssuer(
        builder.Configuration["Jwt:Issuer"]!,
        builder.Configuration["Jwt:Audience"]!,
        builder.Configuration["Jwt:SigningKey"]!,
        builder.Configuration.GetValue<int?>("Jwt:ExpiryMinutes") ?? 60));
builder.Services.AddScoped<IEventStore, PostgresEventStore>();
builder.Services.AddSingleton<IDeviceStatusReadRepository>(sp =>
    new MongoCachedDeviceStatusReadRepository(
        sp.GetRequiredService<IMongoClient>(),
        builder.Configuration["Projection:MongoDatabase"]!,
        sp.GetRequiredService<IConnectionMultiplexer>(),
        builder.Configuration.GetValue<int?>("Cache:DefaultTtlSeconds") ?? 300));
builder.Services.AddSingleton<IDeviceEventProjector>(sp =>
    new MongoDeviceEventProjector(
        sp.GetRequiredService<IMongoClient>(),
        builder.Configuration["Projection:MongoDatabase"]!,
        sp.GetRequiredService<IConnectionMultiplexer>(),
        builder.Configuration.GetValue<int?>("Cache:DefaultTtlSeconds") ?? 300));
builder.Services.AddTransient<RegisterDeviceCommandHandler>();
builder.Services.AddTransient<StartDeviceCommandHandler>();
builder.Services.AddTransient<StopDeviceCommandHandler>();
builder.Services.AddTransient<SetDeviceMaintenanceModeCommandHandler>();
builder.Services.AddTransient<RebuildDeviceReadModelHandler>();
builder.Services.AddTransient<GetAllDevicesQueryHandler>();
builder.Services.AddTransient<GetDeviceStatusQueryHandler>();
builder.Services.AddTransient<RegisterUserCommandHandler>();
builder.Services.AddTransient<LoginUserCommandHandler>();
builder.Services.AddTransient<GetCurrentUserQueryHandler>();
builder.Services.AddTransient<GenerateSimulatedDevicesCommandHandler>();
builder.Services.AddTransient<ResetSimulatedDevicesCommandHandler>();
builder.Services.AddTransient<GetAllSimulatedDevicesQueryHandler>();

builder.Services.Configure<SimulationOptions>(builder.Configuration.GetSection("Simulation"));
builder.Services.AddSingleton<ISimulatedDeviceRepository>(sp =>
    new MongoSimulatedDeviceRepository(
        sp.GetRequiredService<IMongoClient>(),
        builder.Configuration["Projection:MongoDatabase"]!));
builder.Services.AddSingleton<ISimulatedDeviceStateGenerator, DefaultSimulatedDeviceStateGenerator>();
builder.Services.AddSingleton<ISimulatedDeviceSimulationService, Infrastructure.Simulation.SimulatedDeviceSimulationService>();
builder.Services.AddSingleton<SimulationBackgroundService>();
builder.Services.AddHostedService(sp => sp.GetRequiredService<SimulationBackgroundService>());

// 2. Add Swagger/OpenAPI support
builder.Services.AddSwaggerGen(options =>
{
    options.SwaggerDoc("v1", new()
    {
        Version = "v1",
        Title = "Industrial IoT Platform API",
        Description = "An ASP.NET Core Web API for Industrial IoT Platform",
    });
    options.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
    {
        Name = "Authorization",
        Type = SecuritySchemeType.Http,
        Scheme = "bearer",
        BearerFormat = "JWT",
        In = ParameterLocation.Header,
        Description = "Input a valid JWT bearer token."
    });
    options.AddSecurityRequirement(new OpenApiSecurityRequirement
    {
        {
            new OpenApiSecurityScheme
            {
                Reference = new OpenApiReference
                {
                    Type = ReferenceType.SecurityScheme,
                    Id = "Bearer"
                }
            },
            Array.Empty<string>()
        }
    });
});

var app = builder.Build();

await InitializeInfrastructureAsync(app.Services, app.Logger, app.Lifetime.ApplicationStopping);
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
app.UseAuthentication();
app.UseAuthorization();

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
    var jwtIssuer = configuration["Jwt:Issuer"];
    var jwtAudience = configuration["Jwt:Audience"];
    var jwtSigningKey = configuration["Jwt:SigningKey"];

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

    if (string.IsNullOrWhiteSpace(jwtIssuer))
    {
        missing.Add("Jwt:Issuer");
    }

    if (string.IsNullOrWhiteSpace(jwtAudience))
    {
        missing.Add("Jwt:Audience");
    }

    if (string.IsNullOrWhiteSpace(jwtSigningKey))
    {
        missing.Add("Jwt:SigningKey");
    }
    else if (jwtSigningKey.Length < 32)
    {
        throw new InvalidOperationException("Jwt:SigningKey must be at least 32 characters long.");
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

static async Task InitializeInfrastructureAsync(
    IServiceProvider services,
    ILogger logger,
    CancellationToken cancellationToken)
{
    await using var scope = services.CreateAsyncScope();
    var initializer = scope.ServiceProvider.GetRequiredService<InfrastructureDatabaseInitializer>();
    await initializer.InitializeAsync(cancellationToken);
    logger.LogInformation("Infrastructure database schema initialization completed.");
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
