using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using MPM.Core.Data;
using MPM.Shared.Services;

namespace MPM.Core.SystemConfig;

/// <summary>
/// Resuelve el proveedor de IA activo con precedencia BD > env > default (033-migracion-qwen-g4).
/// - BD: tabla system_ai_provider (la escribe el switch del super admin) — persiste entre reinicios.
/// - env: AI:Provider / AI:Endpoint / AI:Model — fallback/bootstrapping (tabla vacía o BD caída).
/// - default: gemini / gemini-2.5-pro.
/// La BD se cachea 30s (consistencia eventual aceptable para un switch operativo); la escritura
/// del switch invalida la cache explícitamente, así el cambio aplica al análisis siguiente.
/// </summary>
public class SystemConfigService(
    ISystemConfigData data,
    IConfiguration config,
    IMemoryCache cache,
    ILogger<SystemConfigService> logger)
{
    private const string CacheKey = "ai_provider_activo";
    private static readonly TimeSpan CacheTtl = TimeSpan.FromSeconds(30);

    public virtual async Task<AiProviderSettings> ObtenerActivoAsync(CancellationToken ct = default)
    {
        if (cache.TryGetValue(CacheKey, out AiProviderSettings? cached) && cached != null)
            return cached;

        var settings = await CargarDesdeFuentesAsync(ct);
        cache.Set(CacheKey, settings, CacheTtl);
        return settings;
    }

    /// <summary>
    /// Persiste un cambio de proveedor (switch del super admin) e invalida la cache para que
    /// el análisis siguiente use el nuevo proveedor sin reiniciar nada.
    /// </summary>
    public virtual async Task<AiProviderSettings> ActualizarAsync(
        string provider, string? endpoint, string model,
        long updatedByUserId, string updatedByUsername,
        CancellationToken ct = default)
    {
        await data.ActualizarAsync(provider, endpoint, model, updatedByUserId, updatedByUsername, ct);

        cache.Remove(CacheKey);
        var settings = new AiProviderSettings(provider, endpoint, model, "database",
            updatedByUsername, DateTime.UtcNow);
        cache.Set(CacheKey, settings, CacheTtl);
        return settings;
    }

    private async Task<AiProviderSettings> CargarDesdeFuentesAsync(CancellationToken ct)
    {
        try
        {
            var row = await data.ObtenerAsync(ct);
            if (row != null)
                return new AiProviderSettings(row.Provider, row.Endpoint, row.Model, "database",
                    row.UpdatedByUsername, row.UpdatedAt);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "No se pudo leer la config de IA desde BD; se usa configuración de entorno");
        }

        var provider = config["AI:Provider"] ?? "gemini";
        var endpoint = config["AI:Endpoint"];
        var model = config["AI:Model"] ?? (provider == "gemini" ? VertexGeminiClient.DefaultModelName : string.Empty);
        return new AiProviderSettings(provider, endpoint, model, "environment");
    }
}
