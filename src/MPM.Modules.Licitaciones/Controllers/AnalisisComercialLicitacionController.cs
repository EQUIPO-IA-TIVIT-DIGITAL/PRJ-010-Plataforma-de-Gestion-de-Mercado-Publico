using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MPM.Modules.Licitaciones.Models;
using MPM.Modules.Licitaciones.Services;
using MPM.Shared.Models;

namespace MPM.Modules.Licitaciones.Controllers;

/// <summary>
/// Zona IA on-demand — análisis comercial de los documentos de una licitación
/// (036-flujo-comercial-ofertas, Fase 1.3). Cache por conjunto de documentos: si la misma
/// versión ya fue analizada, se devuelve sin re-pagar IA.
/// </summary>
[ApiController]
[Route("api/v1/licitaciones/{codigoExterno}/analisis-comercial")]
[Authorize]
public class AnalisisComercialLicitacionController(
    LicitacionService licitacionService,
    AnalisisComercialService analisisComercialService) : ControllerBase
{
    private TenantContext? GetTenant() => HttpContext.Items["TenantContext"] as TenantContext;

    /// <summary>Estado + resultado del análisis comercial (el frontend hace polling mientras "analizando").</summary>
    [HttpGet]
    [ProducesResponseType(typeof(ApiResponse<AnalisisComercialEstadoDto>), 200)]
    [ProducesResponseType(typeof(ApiResponse<object>), 404)]
    public async Task<ActionResult<ApiResponse<AnalisisComercialEstadoDto>>> Estado(
        string codigoExterno, CancellationToken ct = default)
    {
        var lic = await licitacionService.ObtenerPorCodigoAsync(codigoExterno, ct);
        if (lic == null)
            return NotFound(ApiResponse<object>.Fail(
                "Licitación no encontrada",
                [new ErrorDetail { Code = "LIC_001", Message = $"Licitación {codigoExterno} no encontrada" }]));

        var estado = await analisisComercialService.ObtenerEstadoAsync(lic.Id, ct);
        return Ok(ApiResponse<AnalisisComercialEstadoDto>.Ok(estado));
    }

    /// <summary>Dispara el análisis comercial (202 + polling) o devuelve el cacheado (200).</summary>
    [HttpPost]
    [ProducesResponseType(typeof(ApiResponse<IniciarAnalisisComercialResultDto>), 202)]
    [ProducesResponseType(typeof(ApiResponse<IniciarAnalisisComercialResultDto>), 200)]
    [ProducesResponseType(typeof(ApiResponse<object>), 404)]
    [ProducesResponseType(typeof(ApiResponse<object>), 409)]
    [ProducesResponseType(typeof(ApiResponse<object>), 422)]
    public async Task<ActionResult<ApiResponse<IniciarAnalisisComercialResultDto>>> Iniciar(
        string codigoExterno, CancellationToken ct = default)
    {
        var tenant = GetTenant();
        if (tenant == null)
            return Unauthorized(ApiResponse<object>.Fail("No autenticado"));

        var lic = await licitacionService.ObtenerPorCodigoAsync(codigoExterno, ct);
        if (lic == null)
            return NotFound(ApiResponse<object>.Fail(
                "Licitación no encontrada",
                [new ErrorDetail { Code = "LIC_001", Message = $"Licitación {codigoExterno} no encontrada" }]));

        try
        {
            var resultado = await analisisComercialService.IniciarAnalisisAsync(lic.Id, codigoExterno, tenant.UserId, ct);
            return resultado.CacheHit
                ? Ok(ApiResponse<IniciarAnalisisComercialResultDto>.Ok(resultado))
                : StatusCode(202, ApiResponse<IniciarAnalisisComercialResultDto>.Ok(resultado));
        }
        catch (AnalisisComercialService.SinDocumentosException ex)
        {
            return StatusCode(422, ApiResponse<object>.Fail(
                "No se puede analizar",
                [new ErrorDetail { Code = "ANC_001", Message = ex.Message }]));
        }
        catch (AnalisisComercialService.AnalisisEnCursoException ex)
        {
            return Conflict(ApiResponse<object>.Fail(
                "Análisis ya en curso",
                [new ErrorDetail { Code = "ANC_002", Message = ex.Message }]));
        }
        catch (InvalidOperationException ex)
        {
            return StatusCode(422, ApiResponse<object>.Fail(
                "No se pudo iniciar el análisis",
                [new ErrorDetail { Code = "ANC_003", Message = ex.Message }]));
        }
    }
}
