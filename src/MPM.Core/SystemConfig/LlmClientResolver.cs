using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using MPM.Shared.Services;

namespace MPM.Core.SystemConfig;

/// <summary>
/// Resuelve el <see cref="ILlmClient"/> del proveedor activo por request (033-migracion-qwen-g4).
/// Consulta <see cref="SystemConfigService"/> (BD > env > default) y devuelve el cliente
/// registrado por key en DI ("gemini" | "openai"). Los servicios de dominio inyectan este
/// resolver — nunca una implementación concreta ni una instancia fija de arranque.
/// </summary>
public class LlmClientResolver(
    IServiceProvider services,
    SystemConfigService configService,
    ILogger<LlmClientResolver> logger)
{
    /// <summary>Virtual para poder mockearlo en unit tests de los módulos.</summary>
    public virtual async Task<ILlmClient> GetClientAsync(CancellationToken ct = default)
    {
        var settings = await configService.ObtenerActivoAsync(ct);

        var client = services.GetKeyedService<ILlmClient>(settings.Provider);
        if (client == null)
        {
            throw new InvalidOperationException(
                $"El proveedor de IA '{settings.Provider}' no está registrado en el sistema. Proveedores disponibles: gemini, openai.");
        }

        // Los clientes configurables reciben endpoint/modelo resueltos (BD > env) por request,
        // para que el switch del super admin aplique sin reiniciar.
        if (client is IConfigurableLlmClient configurable)
            configurable.ApplySettings(settings.Endpoint, settings.Model);

        logger.LogInformation("Proveedor IA activo: {Provider} (modelo {Model}, fuente {Fuente})",
            settings.Provider, settings.Model, settings.ResolvedFrom);
        return client;
    }
}
