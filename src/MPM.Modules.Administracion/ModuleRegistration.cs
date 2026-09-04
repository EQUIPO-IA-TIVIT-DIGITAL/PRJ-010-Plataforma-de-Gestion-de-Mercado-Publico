using Microsoft.Extensions.DependencyInjection;
using MPM.Modules.Administracion.Data;
using MPM.Modules.Administracion.Services;

namespace MPM.Modules.Administracion;

public static class ModuleRegistration
{
    public static IServiceCollection AddAdministracionModule(this IServiceCollection services)
    {
        services.AddScoped<AdminUsuariosHandler>();
        services.AddScoped<AdminLogsHandler>();
        services.AddScoped<Data.AdminLlmCostosHandler>();
        services.AddScoped<AdminUsuariosService>();
        services.AddScoped<Services.AdminLlmCostosService>();
        return services;
    }
}
