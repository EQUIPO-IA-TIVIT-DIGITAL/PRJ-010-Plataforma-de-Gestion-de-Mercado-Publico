using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using MPM.Modules.Censo.Data;
using MPM.Modules.Censo.Services;

namespace MPM.Modules.Censo;

public static class ModuleRegistration
{
    public static IServiceCollection AddCensoModule(this IServiceCollection services, IConfiguration configuration)
    {
        // Manager de token singleton: cache en memoria + semáforo de renovación única.
        services.AddSingleton<CensusTokenManager>();

        // Cliente HTTP de Census (timeout 100 s: el batch de 16 consultas benchmarkeó 3 s,
        // pero el auth + catálogo + archivos pueden tardar más bajo carga).
        services.AddHttpClient<CensusClient>(client =>
        {
            client.Timeout = TimeSpan.FromSeconds(100);
        });

        services.AddScoped<CensoHandler>();
        services.AddScoped<CensoCatalogoService>();
        services.AddScoped<CensoExpansionService>();
        services.AddScoped<CensoMatchService>();
        return services;
    }
}
