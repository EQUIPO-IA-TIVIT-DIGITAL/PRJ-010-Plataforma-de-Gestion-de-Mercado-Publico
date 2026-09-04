using Microsoft.Extensions.DependencyInjection;
using MPM.Modules.Colaboracion.Data;
using MPM.Modules.Colaboracion.Services;

namespace MPM.Modules.Colaboracion;

public static class ModuleRegistration
{
    public static IServiceCollection AddColaboracionModule(this IServiceCollection services)
    {
        services.AddScoped<LicitacionesInteresHandler>();
        services.AddScoped<LicitacionesInteresService>();

        // 036-flujo-comercial-ofertas (Fase 2): decisión GO/NO GO sobre licitaciones_interes (V144).
        services.AddScoped<DecisionHandler>();
        services.AddScoped<DecisionService>();
        return services;
    }
}
