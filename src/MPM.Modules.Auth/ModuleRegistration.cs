using Microsoft.Extensions.DependencyInjection;
using MPM.Modules.Auth.Data;
using MPM.Shared.Services;

namespace MPM.Modules.Auth;

public static class ModuleRegistration
{
    public static IServiceCollection AddAuthModule(this IServiceCollection services)
    {
        services.AddScoped<AuthHandler>();
        services.AddScoped<IEmailService, SmtpEmailService>();
        return services;
    }
}