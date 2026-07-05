using Microsoft.Extensions.DependencyInjection;

namespace MPM.Modules.Catalogo;

public static class ModuleRegistration
{
    public static IServiceCollection AddCatalogoModule(this IServiceCollection services)
    {
        services.AddScoped<Data.ICatalogoHandler, Data.CatalogoHandler>();
        services.AddScoped<Services.CatalogoService>();
        return services;
    }
}
