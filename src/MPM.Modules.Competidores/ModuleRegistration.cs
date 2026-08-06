using Microsoft.Extensions.DependencyInjection;
using MPM.Modules.Competidores.Data;
using MPM.Modules.Competidores.Services;

namespace MPM.Modules.Competidores;

public static class ModuleRegistration
{
    public static IServiceCollection AddCompetidoresModule(this IServiceCollection services)
    {
        services.AddScoped<OfertasHandler>();
        services.AddScoped<CompetidorAnalisisHandler>();
        services.AddScoped<CompetidoresActividadMercadoHandler>();
        services.AddScoped<CompetidorAnalysisService>();
        services.AddScoped<CompetidorGeminiService>();
        services.AddScoped<CompetidorMercadoService>();
        return services;
    }
}
