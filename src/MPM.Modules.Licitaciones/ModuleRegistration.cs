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

        // 016-extraccion-documentos-api: extracción de documentos vía HTTP directo,
        // con fallback al scraper Node/Playwright existente (Extraccion:Modo).
        services.AddScoped<ExtraccionLogHandler>();
        services.AddScoped<MpSessionProvider>();
        services.AddScoped<WebFormsParser>();
        services.AddScoped<AdjuntosHttpExtractor>();
        services.AddScoped<DocumentExtractionService>();

        // Registrados también como singleton de su propio tipo (no solo IHostedService) para
        // poder resolverlos directamente desde el "modo worker" de Program.cs y llamar
        // EjecutarCicloUnaVezAsync() sin depender del Timer del BackgroundService.
        services.AddSingleton<SyncEngineService>();
        services.AddHostedService(sp => sp.GetRequiredService<SyncEngineService>());
        services.AddSingleton<ScraperBackgroundService>();
        services.AddHostedService(sp => sp.GetRequiredService<ScraperBackgroundService>());
        services.AddHostedService<AclaracionMonitorService>();
        return services;
    }
}