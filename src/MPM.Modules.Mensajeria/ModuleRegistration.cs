using Microsoft.Extensions.DependencyInjection;
using MPM.Modules.Mensajeria.Data;
using MPM.Modules.Mensajeria.Services;

namespace MPM.Modules.Mensajeria;

public static class ModuleRegistration
{
    public static IServiceCollection AddMensajeriaModule(this IServiceCollection services)
    {
        services.AddScoped<ConversacionHandler>();
        services.AddScoped<MensajeHandler>();
        services.AddScoped<AdjuntoHandler>();
        services.AddScoped<PresenciaHandler>();
        services.AddScoped<ConversacionService>();
        services.AddScoped<MensajeService>();
        services.AddScoped<AdjuntoService>();
        services.AddScoped<PresenciaService>();
        return services;
    }
}
