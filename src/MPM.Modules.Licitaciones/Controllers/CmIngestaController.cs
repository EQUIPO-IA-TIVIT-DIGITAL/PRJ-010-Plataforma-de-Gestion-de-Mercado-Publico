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

        // S-001 DoS rango: valida anio dentro de 2020..año+1 (evita enumeración infinita / query sin límite)
        if (anio.HasValue)
        {
            if (anio.Value < 2020 || anio.Value > DateTime.UtcNow.Year + 1)
                return BadRequest(ApiResponse<object>.Fail("VAL_001: anio fuera de rango permitido 2020..año+1", [new ErrorDetail { Code = "VAL_001", Field = "anio", Message = "anio fuera de rango 2020..año+1" }]));

            var row = await cacheHandler.ObtenerPorAnioAsync(rut, anio.Value, ct);
            if (row == null)
                return Ok(ApiResponse<object>.Ok(new { rut, anio = anio.Value, amountClp = 0L, actualizadoAt = (DateTime?)null, payload = (object?)null, cacheHit = false }));
            return Ok(ApiResponse<object>.Ok(new { rut = row.Rut, anio = row.Anio, amountClp = row.AmountClp, actualizadoAt = row.ActualizadoAt, payload = TryParseJson(row.PayloadJson), cacheHit = true }));
        }

        var desde = 2020;
        var hasta = DateTime.UtcNow.Year;
        var rows = await cacheHandler.ObtenerRangoAsync(rut, desde, hasta, ct);
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

        // S-001 DoS rango: validación estricta para evitar ingesta masiva (max 6 años, 2020..año+1)
        if (anio.HasValue)
        {
            if (anio.Value < 2020 || anio.Value > DateTime.UtcNow.Year + 1)
                return BadRequest(ApiResponse<object>.Fail("VAL_001: anio fuera de rango 2020..año+1", [new ErrorDetail { Code = "VAL_001", Field = "anio", Message = "anio fuera de rango 2020..año+1" }]));
        }
        if (desde.HasValue || hasta.HasValue)
        {
            // S-001: rango documentado max 6 años, 2020..año+1
            var d = desde ?? 2020;
            var h = hasta ?? DateTime.UtcNow.Year;
            if (d < 2020 || h > DateTime.UtcNow.Year + 1 || h - d > 5)
                return BadRequest(ApiResponse<object>.Fail("VAL_001: rango max 6 años, 2020..año+1", [new ErrorDetail { Code = "VAL_001", Field = "rango", Message = "rango max 6 años, 2020..año+1" }]));
        }

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
                logger.LogInformation(CmEventIds.SyncOk, "CM sync {Rut} {Anio} -> {Monto}", rut, y, montoCm);
                results.Add(new { anio = y, rut, amountClp = montoCm, ok = true });
            }
            catch (HttpRequestException ex)
            {
                var is429 = ex.StatusCode == System.Net.HttpStatusCode.TooManyRequests;
                if (is429)
                    logger.LogWarning(CmEventIds.Sync429, ex, "CM sync 429 {Rut} {Anio}: {Msg}", rut, y, ex.Message);
                else
                    logger.LogWarning(CmEventIds.Sync5xx, ex, "CM sync fallo {Rut} {Anio}: {Msg}", rut, y, ex.Message);
                results.Add(new { anio = y, rut, amountClp = 0L, ok = false, error = ex.Message });
            }
            catch (Exception ex)
            {
                logger.LogError(CmEventIds.Sync5xx, ex, "CM sync error inesperado {Rut} {Anio}", rut, y);
                return StatusCode(502, ApiResponse<object>.Fail($"Error sync {y}: {ex.Message}"));
            }
        }

        return Ok(ApiResponse<object>.Ok(new { rut, years, results }));
    }

    private static bool EsRutValido(string rut)
    {
        if (string.IsNullOrWhiteSpace(rut)) return false;
        var t = rut.Trim().Replace(".", "").Replace(" ", "").ToUpperInvariant();
        var parts = t.Split('-');
        if (parts.Length != 2) return false;
        var cuerpo = parts[0];
        var dv = parts[1];
        if (cuerpo.Length < 7 || cuerpo.Length > 8) return false;
        if (dv.Length != 1) return false;
        foreach (var c in cuerpo) if (c < '0' || c > '9') return false;
        if (!(dv[0] >= '0' && dv[0] <= '9') && dv[0] != 'K') return false;
        int suma = 0, mult = 2;
        for (int i = cuerpo.Length - 1; i >= 0; i--)
        {
            suma += (cuerpo[i] - '0') * mult;
            mult = mult == 7 ? 2 : mult + 1;
        }
        int resto = 11 - (suma % 11);
        char esperado = resto == 11 ? '0' : resto == 10 ? 'K' : (char)('0' + resto);
        return dv[0] == esperado;
    }

    private static object? TryParseJson(string json)
    {
        try { return JsonSerializer.Deserialize<object>(json); }
        catch { return json; }
    }
}
