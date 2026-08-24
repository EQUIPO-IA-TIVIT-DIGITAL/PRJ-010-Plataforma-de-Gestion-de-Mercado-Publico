using Google.Cloud.Storage.V1;
using MPM.Api.Database;
using MPM.Api.Services;
using MPM.Core.Middleware;
using MPM.Core.Data;
using MPM.Core.Observability;
using MPM.Core.SystemConfig;
using MPM.Modules.Analisis;
using MPM.Modules.Analisis.Health;
using MPM.Modules.Licitaciones;
using MPM.Modules.Licitaciones.Health;
using MPM.Modules.Catalogo;
using MPM.Modules.Mensajeria;
using MPM.Modules.Auth;
using MPM.Modules.Notificaciones;
using MPM.Modules.Mensajeria.Hubs;
using MPM.Modules.Alertas;
using MPM.Modules.Competidores;
using MPM.Modules.Colaboracion;
using MPM.Modules.Administracion;
using MPM.Modules.Administracion.Health;
using MPM.Modules.Censo;
using MPM.Modules.Censo.Health;
using MPM.Modules.Propuestas;
using MPM.Modules.Propuestas.Health;
using MPM.Shared.Services;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.IdentityModel.Tokens;
using StackExchange.Redis;
using System.Text;
using System.Text.Json;
using System.Diagnostics;
using Serilog;
using Serilog.Formatting.Compact;
using MPM.Modules.Licitaciones.Services;
using OpenTelemetry.Trace;
using Prometheus;
using Npgsql;

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

// 037-A: Serilog JSON estructurado + correlationId. Default Information, fallback a Console si falta config.
try
{
    var serilogSection = builder.Configuration.GetSection("Serilog");
    if (serilogSection.Exists())
    {
        Log.Logger = new LoggerConfiguration()
            .ReadFrom.Configuration(builder.Configuration)
            .Enrich.FromLogContext()
            .Enrich.WithProperty("Application", "MPM.Api")
            .CreateLogger();
    }
    else
    {
        Log.Logger = new LoggerConfiguration()
            .MinimumLevel.Information()
            .MinimumLevel.Override("Microsoft", Serilog.Events.LogEventLevel.Warning)
            .MinimumLevel.Override("System", Serilog.Events.LogEventLevel.Warning)
            .Enrich.FromLogContext()
            .Enrich.WithProperty("Application", "MPM.Api")
            .WriteTo.Console(new CompactJsonFormatter())
            .CreateLogger();
    }
    builder.Services.AddSerilog(Log.Logger);
}
catch (Exception ex)
{
    Log.Warning(ex, "Serilog fallback");
    Log.Logger = new LoggerConfiguration()
        .MinimumLevel.Information()
        .MinimumLevel.Override("Microsoft", Serilog.Events.LogEventLevel.Warning)
        .MinimumLevel.Override("System", Serilog.Events.LogEventLevel.Warning)
        .Enrich.FromLogContext()
        .Enrich.WithProperty("Application", "MPM.Api")
        .WriteTo.Console(new CompactJsonFormatter())
        .CreateLogger();
    builder.Services.AddSerilog(Log.Logger);
}

// 037-A: ActivitySource vacío (sin OTel SDK aún, solo registro para 037-B)
builder.Services.AddSingleton(MpmActivitySource.Instance);

// 037-B: OpenTelemetry SDK + W3C propagacion. Feature-flag Otlp:Enabled (default false local).
// Cuando Otlp:Enabled != true no se registra exporter (evita excepcion si collector no existe).
// Endpoint configurable via Otlp:Endpoint (default http://localhost:4317, protocolo gRPC OTLP).
// Traza: MPM.Api ActivitySource + AspNetCore (RecordException=true) + HttpClient + Npgsql.
// Redis via StackExchangeRedis se instrumenta automaticamente cuando se resuelve IConnectionMultiplexer
// (no requiere AddRedisInstrumentation explicito aqui; se deja hook via DiagnosticSource si paquete presente).
var otlpEnabled = builder.Configuration.GetValue<bool>("Otlp:Enabled");
var otlpEndpointRaw = builder.Configuration["Otlp:Endpoint"] ?? "http://localhost:4317";
Uri? otlpEndpoint = Uri.TryCreate(otlpEndpointRaw, UriKind.Absolute, out var _parsed) ? _parsed : null;

builder.Services.AddOpenTelemetry().WithTracing(tracer =>
{
    var b = tracer
        .AddSource(MpmActivitySource.Name)
        .AddAspNetCoreInstrumentation(o => o.RecordException = true)
        .AddHttpClientInstrumentation();

    // Npgsql trace via Npgsql.OpenTelemetry (AddNpgsql()).
    b.AddNpgsql();
    // Redis trace via OpenTelemetry.Instrumentation.StackExchangeRedis.
    // El paquete expone AddRedisInstrumentation(IConnectionMultiplexer) y AddRedisInstrumentation(Action<Options>).
    // Para no duplicar la conexion, usamos el overload de opciones (instrumenta todas las conexiones via DiagnosticSource).
    b.AddRedisInstrumentation(o => o.SetVerboseDatabaseStatements = false);

    if (otlpEnabled && otlpEndpoint != null)
    {
        b.AddOtlpExporter(o => o.Endpoint = otlpEndpoint);
    }
});

// 037-A: Health checks por módulo + agregado (cada uno SELECT 1, sin PII)
builder.Services.AddHealthChecks()
    .AddCheck<LicitacionesHealthCheck>("licitaciones", tags: new[] { "licitaciones" })
    .AddCheck<AnalisisHealthCheck>("analisis", tags: new[] { "analisis" })
    .AddCheck<CensoHealthCheck>("censo", tags: new[] { "censo" })
    .AddCheck<PropuestasHealthCheck>("propuestas", tags: new[] { "propuestas" })
    .AddCheck<AdministracionHealthCheck>("administracion", tags: new[] { "administracion" });

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

// Allow-list de orígenes en vez de SetIsOriginAllowed(_ => true) — cualquier sitio podía hacer
// peticiones autenticadas (con credenciales) contra la API (QA BUG-011). Cors:AllowedOrigins es
// una lista separada por comas; sin configurar, el default cubre solo el frontend local.
var allowedOrigins = (builder.Configuration["Cors:AllowedOrigins"] ?? "http://localhost:3000,http://localhost:8181")
    .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

builder.Services.AddCors(options =>
{
    options.AddDefaultPolicy(policy =>
    {
        policy.WithOrigins(allowedOrigins)
              .AllowAnyMethod()
              .AllowAnyHeader()
              .AllowCredentials();
    });
    options.AddPolicy("SignalR", policy =>
    {
        policy.WithOrigins(allowedOrigins)
              .AllowAnyMethod()
              .AllowAnyHeader()
              .AllowCredentials();
    });
});

builder.Services.AddSingleton<DbConnectionFactory>(_ =>
    new DbConnectionFactory(builder.Configuration.GetConnectionString("PostgreSQL")!));

// 033-migracion-qwen-g4: configuración persistida del proveedor de IA (BD > env > default)
// y resolución dinámica del cliente por request. Los clientes se registran por key
// ("gemini" | "openai"); el resolver elige según el proveedor activo.
builder.Services.AddMemoryCache();
builder.Services.AddSingleton<ISystemConfigData, SystemConfigData>();
builder.Services.AddSingleton<SystemConfigService>();
builder.Services.AddScoped<LlmClientResolver>();
builder.Services.AddKeyedScoped<ILlmClient, VertexGeminiClient>("gemini");
builder.Services.AddKeyedScoped<ILlmClient, OpenAiCompatClient>("openai");

builder.Services.AddSingleton<IConnectionMultiplexer>(_ =>
{
    var connStr = builder.Configuration.GetConnectionString("Redis")!;
    return ConnectionMultiplexer.Connect(connStr);
});

builder.Services.AddScoped<DatabaseInitializer>();
builder.Services.AddSingleton<GoogleAdcTokenProvider>();
// Cliente Gemini compartido entre MPM.Modules.Analisis y MPM.Modules.Competidores
// (029-fix-hallazgos-code-review-competidores-alertas) -- timeout de 5 min porque el análisis
// de PDFs (Análisis) puede tardar bastante más que el análisis de texto de Competidores.
builder.Services.AddHttpClient<VertexGeminiClient>(client =>
{
    client.Timeout = TimeSpan.FromMinutes(5);
});
// 033-migracion-qwen-g4: cliente OpenAI-compatible (Qwen G4, URL entregada por el equipo) --
// mismo timeout de 5 min por el análisis de PDFs multi-documento.
builder.Services.AddHttpClient<OpenAiCompatClient>(client =>
{
    client.Timeout = TimeSpan.FromMinutes(5);
});

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
builder.Services.AddLicitacionModule(builder.Configuration);
builder.Services.AddCensoModule(builder.Configuration);
builder.Services.AddPropuestasModule();
builder.Services.AddCatalogoModule();
builder.Services.AddMensajeriaModule();
builder.Services.AddAnalisisModule();
builder.Services.AddCompetidoresModule();
builder.Services.AddColaboracionModule();
builder.Services.AddAdministracionModule();

var jwtSection = builder.Configuration.GetSection("JWT");
var jwtSecret = jwtSection["Secret"];
if (string.IsNullOrWhiteSpace(jwtSecret) || jwtSecret.Length < 32)
{
    // Antes: fallback embebido en el binario ("default-secret-change-this-in-producion...")
    // si faltaba la config — cualquiera que conociera ese valor podía falsificar sesiones
    // (QA BUG-011). El arranque debe fallar de forma visible, no continuar con un secreto
    // conocido.
    throw new InvalidOperationException(
        "JWT:Secret no está configurado o tiene menos de 32 caracteres. El servicio no puede arrancar sin un secreto de sesión real.");
}
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

app.UseMiddleware<ErrorHandlingMiddleware>();
app.UseMiddleware<TenantMiddleware>();
app.UseSerilogRequestLogging(opts =>
{
    opts.EnrichDiagnosticContext = (diagnosticContext, httpContext) =>
    {
        var cid = httpContext.Items["CorrelationId"] as string ?? httpContext.TraceIdentifier;
        diagnosticContext.Set("CorrelationId", cid);
        diagnosticContext.Set("TraceId", Activity.Current?.TraceId.ToString() ?? cid);
    };
});

app.UseCors();
app.UseAuthentication();
app.UseAuthorization();
// E16 api-versioning: expone versión vigente y deja puerta a Sunset para futuros breaking changes.
// No afecta flujo actual (todo sigue en /api/v1), solo añade headers para trazabilidad.
app.Use(async (ctx, next) =>
{
    ctx.Response.Headers["X-API-Version"] = "v1";
    ctx.Response.Headers["X-API-Supported-Versions"] = "v1";
    // Cuando exista v2, se añadirá Sunset aquí para clientes en v1.
    await next();
});

// 037-A: Health checks públicos, sin auth, nunca exponen PII (solo status + duración)
static Task WriteHealthResponse(HttpContext ctx, HealthReport report)
{
    ctx.Response.ContentType = "application/json";
    ctx.Response.StatusCode = report.Status == HealthStatus.Healthy ? 200 : 503;
    var jsonOpts = new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };
    var path = ctx.Request.Path.Value ?? "";
    var isModule = path.StartsWith("/health/", StringComparison.OrdinalIgnoreCase) && path.Length > 8;
    var basePayload = new
    {
        status = report.Status.ToString().ToLowerInvariant(),
        timestamp = DateTime.UtcNow.ToString("o"),
        checks = report.Entries.ToDictionary(
            e => e.Key,
            e => new
            {
                status = e.Value.Status.ToString().ToLowerInvariant(),
                durationMs = Math.Round(e.Value.Duration.TotalMilliseconds, 2)
            }),
        totalDurationMs = Math.Round(report.TotalDuration.TotalMilliseconds, 2)
    };
    if (isModule)
    {
        var module = path.Split('/', StringSplitOptions.RemoveEmptyEntries).LastOrDefault() ?? "unknown";
        var payload = new
        {
            status = basePayload.status,
            module,
            timestamp = basePayload.timestamp,
            checks = basePayload.checks,
            totalDurationMs = basePayload.totalDurationMs
        };
        return ctx.Response.WriteAsync(JsonSerializer.Serialize(payload, jsonOpts));
    }
    return ctx.Response.WriteAsync(JsonSerializer.Serialize(basePayload, jsonOpts));
}

app.MapHealthChecks("/health", new HealthCheckOptions
{
    AllowCachingResponses = false,
    Predicate = _ => true,
    ResponseWriter = WriteHealthResponse
});
app.MapHealthChecks("/health/licitaciones", new HealthCheckOptions
{
    AllowCachingResponses = false,
    Predicate = r => r.Name == "licitaciones",
    ResponseWriter = WriteHealthResponse
});
app.MapHealthChecks("/health/analisis", new HealthCheckOptions
{
    AllowCachingResponses = false,
    Predicate = r => r.Name == "analisis",
    ResponseWriter = WriteHealthResponse
});
app.MapHealthChecks("/health/censo", new HealthCheckOptions
{
    AllowCachingResponses = false,
    Predicate = r => r.Name == "censo",
    ResponseWriter = WriteHealthResponse
});
app.MapHealthChecks("/health/propuestas", new HealthCheckOptions
{
    AllowCachingResponses = false,
    Predicate = r => r.Name == "propuestas",
    ResponseWriter = WriteHealthResponse
});
app.MapHealthChecks("/health/administracion", new HealthCheckOptions
{
    AllowCachingResponses = false,
    Predicate = r => r.Name == "administracion",
    ResponseWriter = WriteHealthResponse
});

app.MapControllers();
app.MapHub<MensajeriaHub>("/hubs/mensajeria").RequireCors("SignalR");

// 037-B: prometheus-net /metrics - solo interno, AllowAnonymous sin CORS publico, sin PII labels (OBS-R005).
// Se usa MapMetrics (endpoint routing) en vez de UseMetricServer; documentado como eleccion.
// MpmMetrics static en MPM.Core/Observability/MpmMetrics.cs define mpm_http_requests_total, etc - solo declara, incremento en 037-C.
// Warmup: toca los contadores para que prometheus registre HELP/TYPE aunque aun no hay observaciones.
_ = MpmMetrics.HttpRequestsTotal;
_ = MpmMetrics.HttpDurationSeconds;
_ = MpmMetrics.LlmCallsTotal;
_ = MpmMetrics.LlmTokensTotal;
_ = MpmMetrics.LlmLatencySeconds;
_ = MpmMetrics.SyncLicitacionesTotal;
_ = MpmMetrics.AclaracionesDetectadasTotal;
_ = MpmMetrics.ScraperRunsTotal;

app.MapMetrics();

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
    builder.Services.AddHttpClient<VertexGeminiClient>(client =>
    {
        client.Timeout = TimeSpan.FromMinutes(5);
    });
    builder.Services.AddHttpClient<OpenAiCompatClient>(client =>
    {
        client.Timeout = TimeSpan.FromMinutes(5);
    });
    // 033-migracion-qwen-g4: mismo wiring de proveedor IA que el servicio web (el worker
    // resuelve AnalisisModule → GeminiService → LlmClientResolver → SystemConfigService).
    builder.Services.AddMemoryCache();
    builder.Services.AddSingleton<ISystemConfigData, SystemConfigData>();
    builder.Services.AddSingleton<SystemConfigService>();
    builder.Services.AddScoped<LlmClientResolver>();
    builder.Services.AddKeyedScoped<ILlmClient, VertexGeminiClient>("gemini");
    builder.Services.AddKeyedScoped<ILlmClient, OpenAiCompatClient>("openai");

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
    builder.Services.AddLicitacionModule(builder.Configuration);
    builder.Services.AddCensoModule(builder.Configuration);
    builder.Services.AddPropuestasModule();
    builder.Services.AddCatalogoModule();
    builder.Services.AddAnalisisModule();
    // Worker de backfill de areas de negocio (WORKER_MODE=backfill-areas) — se registra
    // aca porque solo lo usa el modo worker, no el servicio web.
    builder.Services.AddScoped<AreasBackfillService>();

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
            case "backfill-areas":
                // 2026-08-13: backfill de areas_negocio (V136) fuera del arranque web --
                // el intento dentro de DatabaseInitializer crasheo la instancia de Cloud Run
                // (signal 11 / OOM con 512Mi) y supero el startup timeout. Corre como Cloud
                // Run Job con memoria propia; la plataforma arranca sin esperarlo.
                await scope.ServiceProvider.GetRequiredService<AreasBackfillService>().EjecutarAsync();
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
