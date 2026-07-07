using Google.Cloud.Storage.V1;
using MPM.Api.Database;
using MPM.Api.Services;
using MPM.Core.Middleware;
using MPM.Core.Data;
using MPM.Modules.Analisis;
using MPM.Modules.Licitaciones;
using MPM.Modules.Catalogo;
using MPM.Modules.Mensajeria;
using MPM.Modules.Auth;
using MPM.Modules.Notificaciones;
using MPM.Modules.Mensajeria.Hubs;
using MPM.Modules.Alertas;
using MPM.Shared.Services;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using StackExchange.Redis;
using System.Text;
using MPM.Modules.Licitaciones.Services;

Dapper.DefaultTypeMap.MatchNamesWithUnderscores = true;

// Modo worker (Cloud Run Job): ejecuta un solo ciclo de un background service y termina,
// en vez de levantar Kestrel. Ver specs/002-fase5-deploy-gcp/plan.md T008.
// AnalisisBackgroundService queda fuera de este mecanismo a propósito: no es un Timer/loop
// periódico sino un disparo fire-and-forget por cada documento subido (ver su código) — pasar
// eso a un Cloud Run Job requiere rediseñarlo como consumidor de Pub/Sub, no solo exponer un
// "ejecutar una vez". Ese rediseño queda pendiente (ver research.md de 002-fase5-deploy-gcp).
var workerMode = Environment.GetEnvironmentVariable("WORKER_MODE");
if (!string.IsNullOrWhiteSpace(workerMode))
{
    var exitCode = await EjecutarWorkerAsync(workerMode, args);
    Environment.Exit(exitCode);
}

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new() { Title = "MPM API", Version = "v1" });
    var xmlFiles = Directory.GetFiles(AppContext.BaseDirectory, "*.xml", SearchOption.TopDirectoryOnly);
    foreach (var xmlFile in xmlFiles)
    {
        c.IncludeXmlComments(xmlFile);
    }
});

builder.Services.AddCors(options =>
{
    options.AddDefaultPolicy(policy =>
    {
        policy.SetIsOriginAllowed(_ => true)
              .AllowAnyMethod()
              .AllowAnyHeader()
              .AllowCredentials();
    });
    options.AddPolicy("SignalR", policy =>
    {
        policy.SetIsOriginAllowed(_ => true)
              .AllowAnyMethod()
              .AllowAnyHeader()
              .AllowCredentials();
    });
});

builder.Services.AddSingleton<DbConnectionFactory>(_ =>
    new DbConnectionFactory(builder.Configuration.GetConnectionString("PostgreSQL")!));

builder.Services.AddSingleton<IConnectionMultiplexer>(_ =>
{
    var connStr = builder.Configuration.GetConnectionString("Redis")!;
    return ConnectionMultiplexer.Connect(connStr);
});

builder.Services.AddScoped<DatabaseInitializer>();
builder.Services.AddSingleton<GoogleAdcTokenProvider>();

var storageProvider = builder.Configuration.GetValue<string>("Storage:Provider") ?? "local";
if (storageProvider == "gcs")
{
    var bucketName = builder.Configuration.GetValue<string>("Storage:Bucket") ?? "tivit-cu010-mpm-adjuntos";
    builder.Services.AddSingleton(StorageClient.Create());
    builder.Services.AddSingleton<IStorageService>(sp =>
        new GcsStorageService(sp.GetRequiredService<StorageClient>(), bucketName));
}
else
{
    builder.Services.AddSingleton<IStorageService, LocalStorageService>();
}

builder.Services.AddAuthModule();
builder.Services.AddNotificacionesModule();
builder.Services.AddAlertasModule();
builder.Services.AddLicitacionModule();
builder.Services.AddCatalogoModule();
builder.Services.AddMensajeriaModule();
builder.Services.AddAnalisisModule();

var jwtSection = builder.Configuration.GetSection("JWT");
var jwtSecret = jwtSection["Secret"] ?? "default-secret-change-this-in-production-min-32-chars";
var jwtIssuer = jwtSection["Issuer"] ?? "TIVIT.MPM";

builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtSecret)),
            ValidateIssuer = true,
            ValidIssuer = jwtIssuer,
            ValidateAudience = false,
            ValidateLifetime = true,
            ClockSkew = TimeSpan.Zero
        };
        options.Events = new JwtBearerEvents
        {
            OnMessageReceived = context =>
            {
                var accessToken = context.Request.Query["access_token"];
                var path = context.HttpContext.Request.Path;
                if (!string.IsNullOrEmpty(accessToken) &&
                    path.StartsWithSegments("/hubs"))
                {
                    context.Token = accessToken;
                }
                return Task.CompletedTask;
            }
        };
    });

builder.Services.AddAuthorization();

builder.Services.AddSignalR()
    .AddStackExchangeRedis(builder.Configuration.GetConnectionString("Redis")!);

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseCors();
app.UseMiddleware<ErrorHandlingMiddleware>();
app.UseAuthentication();
app.UseMiddleware<TenantMiddleware>();
app.UseAuthorization();
app.MapControllers();
app.MapHub<MensajeriaHub>("/hubs/mensajeria").RequireCors("SignalR");

using (var scope = app.Services.CreateScope())
{
    var initializer = scope.ServiceProvider.GetRequiredService<DatabaseInitializer>();
    await initializer.InitializeAsync();
}

app.Run();

/// <summary>
/// Construye el mismo contenedor de DI que el servicio web (sin levantar Kestrel), ejecuta
/// un solo ciclo del background service pedido por <c>WORKER_MODE</c>, y retorna un código
/// de salida. Pensado para correr como Cloud Run Job (<c>sync-job</c>, <c>scraper-job</c>).
/// No corre <see cref="DatabaseInitializer"/> — se asume que el servicio web ya aplicó las
/// migraciones; correrlas desde cada ejecución de un Job sería redundante y riesgoso si dos
/// Jobs corrieran en paralelo.
/// </summary>
static async Task<int> EjecutarWorkerAsync(string workerMode, string[] args)
{
    var builder = WebApplication.CreateBuilder(args);

    builder.Services.AddSingleton<DbConnectionFactory>(_ =>
        new DbConnectionFactory(builder.Configuration.GetConnectionString("PostgreSQL")!));
    builder.Services.AddSingleton<IConnectionMultiplexer>(_ =>
        ConnectionMultiplexer.Connect(builder.Configuration.GetConnectionString("Redis")!));
    builder.Services.AddSingleton<GoogleAdcTokenProvider>();

    var storageProvider = builder.Configuration.GetValue<string>("Storage:Provider") ?? "local";
    if (storageProvider == "gcs")
    {
        var bucketName = builder.Configuration.GetValue<string>("Storage:Bucket") ?? "tivit-cu010-mpm-adjuntos";
        builder.Services.AddSingleton(StorageClient.Create());
        builder.Services.AddSingleton<IStorageService>(sp =>
            new GcsStorageService(sp.GetRequiredService<StorageClient>(), bucketName));
    }
    else
    {
        builder.Services.AddSingleton<IStorageService, LocalStorageService>();
    }

    builder.Services.AddNotificacionesModule();
    builder.Services.AddAlertasModule();
    builder.Services.AddLicitacionModule();
    builder.Services.AddCatalogoModule();
    builder.Services.AddAnalisisModule();

    using var app = builder.Build();
    using var scope = app.Services.CreateScope();
    var logger = scope.ServiceProvider.GetRequiredService<ILogger<Program>>();

    try
    {
        switch (workerMode)
        {
            case "sync":
                await scope.ServiceProvider.GetRequiredService<SyncEngineService>().EjecutarCicloUnaVezAsync();
                break;
            case "scraper":
                await scope.ServiceProvider.GetRequiredService<ScraperBackgroundService>().EjecutarCicloUnaVezAsync();
                break;
            default:
                logger.LogError("WORKER_MODE desconocido: {Modo}. Valores válidos: sync, scraper", workerMode);
                return 1;
        }

        logger.LogInformation("Ciclo de worker '{Modo}' completado.", workerMode);
        return 0;
    }
    catch (Exception ex)
    {
        logger.LogError(ex, "Ciclo de worker '{Modo}' falló.", workerMode);
        return 1;
    }
}