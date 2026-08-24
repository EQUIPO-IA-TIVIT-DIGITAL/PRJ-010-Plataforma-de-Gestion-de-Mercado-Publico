using System.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using MPM.Core.Observability;
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
        // 037-C: Activity padre para resolución LLM (sin PII) - el hijo llm.call lo crea cada cliente
        using var activity = MpmActivitySource.Instance.StartActivity("llm.resolve", ActivityKind.Internal);
        var settings = await configService.ObtenerActivoAsync(ct);

        activity?.SetTag("llm.provider", settings.Provider);
        activity?.SetTag("llm.modelo", settings.Model);
        activity?.SetTag("llm.fuente", settings.ResolvedFrom);

        var client = services.GetKeyedService<ILlmClient>(settings.Provider);
        if (client == null)
        {
            activity?.SetStatus(ActivityStatusCode.Error, "provider not registered");
            throw new InvalidOperationException(
                $"El proveedor de IA '{settings.Provider}' no está registrado en el sistema. Proveedores disponibles: gemini, openai.");
        }

        if (client is IConfigurableLlmClient configurable)
            configurable.ApplySettings(settings.Endpoint, settings.Model);

        logger.LogInformation("Proveedor IA activo: {Provider} (modelo {Model}, fuente {Fuente})",
            settings.Provider, settings.Model, settings.ResolvedFrom);
        activity?.SetStatus(ActivityStatusCode.Ok);
        return client;
    }
}
