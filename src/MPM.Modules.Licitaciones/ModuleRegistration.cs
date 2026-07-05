using Microsoft.Extensions.DependencyInjection;
using MPM.Modules.Licitaciones.Data;
using MPM.Modules.Licitaciones.Services;

namespace MPM.Modules.Licitaciones;

public static class ModuleRegistration
{
    public static IServiceCollection AddLicitacionModule(this IServiceCollection services)
    {
        services.AddScoped<LicitacionHandler>();
        services.AddScoped<SyncLogHandler>();
        services.AddScoped<SyncEngineHandler>();
        services.AddScoped<SeguimientoHandler>();
        services.AddScoped<LicitacionService>();
        services.AddScoped<SyncService>();
        services.AddHttpClient<ApiMpService>();
        services.AddHostedService<SyncEngineService>();
        services.AddHostedService<ScraperBackgroundService>();
        services.AddHostedService<AclaracionMonitorService>();
        return services;
    }
}