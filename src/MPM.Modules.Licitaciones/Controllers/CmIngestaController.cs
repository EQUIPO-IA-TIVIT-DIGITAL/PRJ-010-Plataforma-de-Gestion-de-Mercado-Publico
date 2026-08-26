using System.Text.Json;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using MPM.Modules.Licitaciones.Services;
using MPM.Shared.Models;
using MPM.Shared.Services;

namespace MPM.Modules.Licitaciones.Controllers;

/// <summary>
/// Track2 ligero — admin ingesta CM vía mserv API (ADR-016 opción B sin zip).
/// GET /api/v1/admin/ingesta-cm/resumen?rut=76.130.712-6[&anio=2026]
/// POST /api/v1/admin/ingesta-cm/sync?rut=76.130.712-6[&anio=2026|&desde=2024&hasta=2026]
/// </summary>
[ApiController]
[Route("api/v1/admin/ingesta-cm")]
[Authorize(Roles = "Admin,SuperAdmin")]
public class CmIngestaController(
    ChileCompraMservService mservService,
    ICmResumenHandler cacheHandler,
    ILogger<CmIngestaController> logger) : ControllerBase
{
    /// <summary>
    /// Resumen cacheado. Si anio provisto -> fila única; si no -> rango 2020..año actual.
    /// </summary>
    [HttpGet("resumen")]
    [ProducesResponseType(typeof(ApiResponse<object>), 200)]
    public async Task<ActionResult<ApiResponse<object>>> GetResumen(
        [FromQuery] string rut = "76.130.712-6",
        [FromQuery] int? anio = null,
        CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(rut) || !EsRutValido(rut))
            return BadRequest(ApiResponse<object>.Fail("RUT inválido", [new ErrorDetail { Code = "VAL_001", Field = "rut", Message = "RUT con formato inválido" }]));

        if (anio.HasValue)
        {
            var row = await cacheHandler.ObtenerPorAnioAsync(rut, anio.Value, ct)
                      ?? await cacheHandler.ObtenerPorAnioAsync(anio.Value, ct);
            if (row == null)
                return Ok(ApiResponse<object>.Ok(new { rut, anio = anio.Value, amountClp = 0L, actualizadoAt = (DateTime?)null, payload = (object?)null, cacheHit = false }));
            return Ok(ApiResponse<object>.Ok(new { rut = row.Rut, anio = row.Anio, amountClp = row.AmountClp, actualizadoAt = row.ActualizadoAt, payload = TryParseJson(row.PayloadJson), cacheHit = true }));
        }

        var desde = 2020;
        var hasta = DateTime.UtcNow.Year;
        var rows = await cacheHandler.ObtenerRangoAsync(rut, desde, hasta, ct);
        // fallback: si tabla usa PK anio sin rut, el rango por rut puede venir vacío pero hay filas con otro rut literal
        if (rows.Count == 0)
        {
            // intenta lectura directa sin filtro rut
            rows = await cacheHandler.ObtenerRangoAsync(rut, desde, hasta, ct);
        }
        var data = rows.Select(r => new { rut = r.Rut, anio = r.Anio, amountClp = r.AmountClp, actualizadoAt = r.ActualizadoAt, payload = TryParseJson(r.PayloadJson) }).ToList();
        return Ok(ApiResponse<object>.Ok(new { rut, desde, hasta, total = data.Count, items = data }));
    }

    /// <summary>
    /// Dispara sync contra mserv y upserta cache.
    /// Query: rut, anio (single) o desde/hasta (rango). Sin params -> año actual.
    /// </summary>
    [HttpPost("sync")]
    [ProducesResponseType(typeof(ApiResponse<object>), 200)]
    public async Task<ActionResult<ApiResponse<object>>> Sync(
        [FromQuery] string rut = "76.130.712-6",
        [FromQuery] int? anio = null,
        [FromQuery] int? desde = null,
        [FromQuery] int? hasta = null,
        CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(rut) || !EsRutValido(rut))
            return BadRequest(ApiResponse<object>.Fail("RUT inválido", [new ErrorDetail { Code = "VAL_001", Field = "rut", Message = "RUT con formato inválido" }]));

        var years = new List<int>();
        if (anio.HasValue) years.Add(anio.Value);
        else if (desde.HasValue || hasta.HasValue)
        {
            var d = desde ?? 2020;
            var h = hasta ?? DateTime.UtcNow.Year;
            if (d > h) return BadRequest(ApiResponse<object>.Fail("Rango inválido: desde > hasta"));
            for (var y = d; y <= h; y++) years.Add(y);
        }
        else
        {
            years.Add(DateTime.UtcNow.Year);
        }

        var results = new List<object>();
        foreach (var y in years)
        {
            try
            {
                var modalities = await mservService.GetModalityAsync(y, rut, ct);
                var montoCm = modalities.FirstOrDefault(m => m.IdModalidad == 5)?.AmountCLPAnnual ?? 0L;
                var payloadJson = JsonSerializer.Serialize(modalities);
                await cacheHandler.UpsertCacheAsync(y, rut, montoCm, payloadJson, ct);
                logger.LogInformation("CM sync {Rut} {Anio} -> {Monto}", rut, y, montoCm);
                results.Add(new { anio = y, rut, amountClp = montoCm, ok = true });
            }
            catch (HttpRequestException ex)
            {
                logger.LogWarning(ex, "CM sync fallo {Rut} {Anio}: {Msg}", rut, y, ex.Message);
                results.Add(new { anio = y, rut, amountClp = 0L, ok = false, error = ex.Message });
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "CM sync error inesperado {Rut} {Anio}", rut, y);
                return StatusCode(502, ApiResponse<object>.Fail($"Error sync {y}: {ex.Message}"));
            }
        }

        return Ok(ApiResponse<object>.Ok(new { rut, years, results }));
    }

    private static bool EsRutValido(string rut)
    {
        var t = rut.Trim();
        return t.Length >= 8 && t.Contains('-');
    }

    private static object? TryParseJson(string json)
    {
        try { return JsonSerializer.Deserialize<object>(json); }
        catch { return json; }
    }
}
