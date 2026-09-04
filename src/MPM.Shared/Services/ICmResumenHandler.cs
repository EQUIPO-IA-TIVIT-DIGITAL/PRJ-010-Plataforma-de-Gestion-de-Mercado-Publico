using MPM.Shared.Models;

namespace MPM.Shared.Services;

/// <summary>
/// Track2 ligero — cache agregada CM desde mserv (ADR-016 opción B sin zip).
/// Persistencia mínima: cm_resumen_api_cache + SP upsert. Ver V159.
/// </summary>
public interface ICmResumenHandler
{
    Task UpsertCacheAsync(int anio, string rut, long amountClp, string payloadJson, CancellationToken ct = default);

    Task<CmResumenCacheDto?> ObtenerPorAnioAsync(int anio, CancellationToken ct = default);

    Task<CmResumenCacheDto?> ObtenerPorAnioAsync(string rut, int anio, CancellationToken ct = default);

    Task<IReadOnlyList<CmResumenCacheDto>> ObtenerRangoAsync(string rut, int anioDesde, int anioHasta, CancellationToken ct = default);

    Task<long> ObtenerMontoAnualAsync(string rut, int anio, CancellationToken ct = default);

    // Alias requerido por spec task
    Task<IReadOnlyList<CmResumenCacheDto>> ObtenerResumenAnualAsync(string rut, int anioDesde, int anioHasta, CancellationToken ct = default);
}
