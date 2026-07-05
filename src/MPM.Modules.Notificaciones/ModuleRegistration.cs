using Microsoft.Extensions.DependencyInjection;
using MPM.Modules.Notificaciones.Data;
using MPM.Modules.Notificaciones.Services;

namespace MPM.Modules.Notificaciones;

public static class ModuleRegistration
{
    public static IServiceCollection AddNotificacionesModule(this IServiceCollection services)
    {
        services.AddScoped<NotificacionesHandler>();
        services.AddScoped<NotificacionesService>();
        return services;
    }
}
