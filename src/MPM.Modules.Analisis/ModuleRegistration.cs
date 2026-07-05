using Microsoft.Extensions.DependencyInjection;
using MPM.Modules.Analisis.Data;
using MPM.Modules.Analisis.Services;

namespace MPM.Modules.Analisis;

public static class ModuleRegistration
{
    public static IServiceCollection AddAnalisisModule(this IServiceCollection services)
    {
        services.AddScoped<AnalisisHandler>();
        services.AddHttpClient<GeminiService>(client =>
        {
            client.Timeout = TimeSpan.FromMinutes(5);
        });
        services.AddScoped<AnalisisService>();
        services.AddSingleton<IAnalisisBackgroundService, AnalisisBackgroundService>();
        return services;
    }
}
