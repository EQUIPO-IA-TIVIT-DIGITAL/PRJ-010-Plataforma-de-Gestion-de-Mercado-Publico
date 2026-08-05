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
        return services;
    }
}
