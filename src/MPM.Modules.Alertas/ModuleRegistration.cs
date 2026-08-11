using Microsoft.Extensions.DependencyInjection;
using MPM.Modules.Alertas.Data;
using MPM.Modules.Alertas.Services;

namespace MPM.Modules.Alertas;

public static class ModuleRegistration
{
    public static IServiceCollection AddAlertasModule(this IServiceCollection services)
    {
        services.AddScoped<AlertasHandler>();
        services.AddScoped<AlertasService>();
        services.AddScoped<AlertaEnriquecimientoService>();
        services.AddScoped<AlertasMatchingService>();
        // 033-migracion-qwen-g4: SinonimosIaService ya no hace HTTP propio -- resuelve el
        // cliente de IA activo vía LlmClientResolver (registrado en Program.cs).
        services.AddScoped<SinonimosIaService>();
        // Timeout explícito (default de HttpClient es 100s) — un Telegram lento no debe colgar
        // el resto del ciclo de matching (QA BUG-013).
        services.AddHttpClient<TelegramNotificationService>(c => c.Timeout = TimeSpan.FromSeconds(10));
        services.AddHttpClient<ResumenLicitacionService>(c => c.Timeout = TimeSpan.FromSeconds(15));
        services.AddScoped<EmailNotificationService>();
        return services;
    }
}
