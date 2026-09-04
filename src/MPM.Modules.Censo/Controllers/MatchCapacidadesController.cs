using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MPM.Modules.Censo.Models;
using MPM.Modules.Censo.Services;
using MPM.Modules.Licitaciones.Services;
using MPM.Shared.Models;

namespace MPM.Modules.Censo.Controllers;

/// <summary>
/// Match de capacidades TIVIT contra Census (036-flujo-comercial-ofertas, Fase 2).
/// POST síncrono (~3 s benchmark D7.10; ~0 ms con cache caliente); GET lee el último
/// resultado persistido sin consultar Census.
/// </summary>
[ApiController]
[Route("api/v1/licitaciones/{codigoExterno}/match-capacidades")]
[Authorize]
public class MatchCapacidadesController(
    LicitacionService licitacionService,
    CensoMatchService matchService) : ControllerBase
{
    private TenantContext? GetTenant() => HttpContext.Items["TenantContext"] as TenantContext;

    /// <summary>Ejecuta el match (body opcional: body > análisis > CEN_004/CEN_001).</summary>
    [HttpPost]
    [ProducesResponseType(typeof(ApiResponse<CensoMatchResultDto>), 200)]
    [ProducesResponseType(typeof(ApiResponse<object>), 404)]
    [ProducesResponseType(typeof(ApiResponse<object>), 409)]
    [ProducesResponseType(typeof(ApiResponse<object>), 422)]
    [ProducesResponseType(typeof(ApiResponse<object>), 502)]
    public async Task<ActionResult<ApiResponse<CensoMatchResultDto>>> Ejecutar(
        string codigoExterno, [FromBody] CensoMatchRequest? request, CancellationToken ct = default)
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
            var resultado = await matchService.EjecutarMatchAsync(lic.Id, tenant.UserId, request, ct);
            return Ok(ApiResponse<CensoMatchResultDto>.Ok(resultado));
        }
        catch (CensoMatchService.SinAnalisisException ex)
        {
            return StatusCode(422, ApiResponse<object>.Fail(
                "Sin documentos analizados",
                [new ErrorDetail { Code = "CEN_004", Message = ex.Message }]));
        }
        catch (CensoMatchService.SinRequisitosException ex)
        {
            return StatusCode(422, ApiResponse<object>.Fail(
                "Sin requisitos para el match",
                [new ErrorDetail { Code = "CEN_001", Message = ex.Message }]));
        }
        catch (CensoMatchService.MatchEnCursoException ex)
        {
            return Conflict(ApiResponse<object>.Fail(
                "Match en curso",
                [new ErrorDetail { Code = "CEN_003", Message = ex.Message }]));
        }
        catch (CensoMatchService.CensusInaccesibleException ex)
        {
            return StatusCode(502, ApiResponse<object>.Fail(
                "Census inalcanzable",
                [new ErrorDetail { Code = "CEN_002", Message = ex.Message }]));
        }
    }

    /// <summary>Estado + último resultado del match (lectura local, sin Census).</summary>
    [HttpGet]
    [ProducesResponseType(typeof(ApiResponse<CensoMatchEstadoDto>), 200)]
    [ProducesResponseType(typeof(ApiResponse<object>), 404)]
    public async Task<ActionResult<ApiResponse<CensoMatchEstadoDto>>> Obtener(
        string codigoExterno, CancellationToken ct = default)
    {
        var lic = await licitacionService.ObtenerPorCodigoAsync(codigoExterno, ct);
        if (lic == null)
            return NotFound(ApiResponse<object>.Fail(
                "Licitación no encontrada",
                [new ErrorDetail { Code = "LIC_001", Message = $"Licitación {codigoExterno} no encontrada" }]));

        var match = await matchService.ObtenerMatchAsync(lic.Id, ct);
        var estado = new CensoMatchEstadoDto
        {
            Estado = match == null ? "no_ejecutado" : "completado",
            UltimoEjecutadoAt = match?.EjecutadoEn,
            Match = match,
        };

        return Ok(ApiResponse<CensoMatchEstadoDto>.Ok(estado));
    }
}
