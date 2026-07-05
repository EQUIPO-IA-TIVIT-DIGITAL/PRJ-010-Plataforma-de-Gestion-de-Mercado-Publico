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
using MPM.Shared.Services;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using StackExchange.Redis;
using System.Text;

Dapper.DefaultTypeMap.MatchNamesWithUnderscores = true;

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
builder.Services.AddLicitacionModule();
builder.Services.AddCatalogoModule();
builder.Services.AddMensajeriaModule();
builder.Services.AddAnalisisModule();
builder.Services.AddNotificacionesModule();

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