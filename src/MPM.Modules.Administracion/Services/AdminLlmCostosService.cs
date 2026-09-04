using MPM.Modules.Administracion.Data;
using MPM.Modules.Administracion.Models;

namespace MPM.Modules.Administracion.Services;

/// <summary>
/// 037-C: Servicio de consulta de costos LLM para SuperAdmin.
/// Valida rango de fechas (max 90 días para evitar cardinalidad) y delega al handler.
/// No expone prompts ni PII (solo agregado por provider/modelo/día).
/// </summary>
public class AdminLlmCostosService(AdminLlmCostosHandler handler)
{
    private readonly AdminLlmCostosHandler _handler = handler;

    public async Task<IEnumerable<LlmCostoDiaDto>> ResumenAsync(
        DateOnly? desde, DateOnly? hasta, CancellationToken ct = default)
    {
        // Defaults: si no se especifica rango, últimos 30 días
        var hoy = DateOnly.FromDateTime(DateTime.UtcNow);
        desde ??= hoy.AddDays(-30);
        hasta ??= hoy;

        if (desde > hasta)
            throw new InvalidOperationException("La fecha 'desde' no puede ser posterior a 'hasta'.");

        var dias = hasta.Value.DayNumber - desde.Value.DayNumber;
        if (dias > 365)
            throw new InvalidOperationException("El rango máximo es de 365 días. Usa un intervalo más corto.");

        return await _handler.ResumenAsync(desde, hasta, ct);
    }
}
