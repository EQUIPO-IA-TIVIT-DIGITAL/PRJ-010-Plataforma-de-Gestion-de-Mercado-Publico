using Microsoft.Extensions.DependencyInjection;
using MPM.Modules.Analisis.Data;
using MPM.Modules.Analisis.Services;

namespace MPM.Modules.Analisis;

public static class ModuleRegistration
{
    public static IServiceCollection AddAnalisisModule(this IServiceCollection services)
    {
        services.AddScoped<AnalisisHandler>();
        services.AddScoped<GeminiService>();
        services.AddScoped<AnalisisService>();
        services.AddSingleton<IAnalisisBackgroundService, AnalisisBackgroundService>();
        services.AddHostedService<AnalisisRecoveryWorker>();
        return services;
    }
}
