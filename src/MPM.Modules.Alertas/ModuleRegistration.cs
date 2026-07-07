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
        services.AddHttpClient<SinonimosIaService>();
        services.AddHttpClient<TelegramNotificationService>();
        return services;
    }
}
