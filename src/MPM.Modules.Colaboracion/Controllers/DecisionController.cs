using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MPM.Modules.Colaboracion.Models;
using MPM.Modules.Colaboracion.Services;
using MPM.Modules.Licitaciones.Services;
using MPM.Shared.Models;

namespace MPM.Modules.Colaboracion.Controllers;

/// <summary>
/// Decisión formal GO/NO GO de una licitación (036-flujo-comercial-ofertas, Fase 2).
/// Siempre humana: el body trae {decision, motivo}; la recomendación IA se copia como
/// snapshot del último análisis comercial completado al momento de decidir (V142 → V144).
/// </summary>
[ApiController]
[Route("api/v1/licitaciones/{codigoExterno}/decision")]
[Authorize]
public class DecisionController(
    LicitacionService licitacionService,
    DecisionService decisionService) : ControllerBase
{
    private TenantContext? GetTenant() => HttpContext.Items["TenantContext"] as TenantContext;

    /// <summary>Registra (o reemplaza) la decisión del gerente.</summary>
    [HttpPost]
    [ProducesResponseType(typeof(ApiResponse<DecisionDto>), 200)]
    [ProducesResponseType(typeof(ApiResponse<object>), 400)]
    [ProducesResponseType(typeof(ApiResponse<object>), 404)]
    [ProducesResponseType(typeof(ApiResponse<object>), 422)]
    public async Task<ActionResult<ApiResponse<DecisionDto>>> Registrar(
        string codigoExterno, [FromBody] DecisionRequest request, CancellationToken ct = default)
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
            var dto = await decisionService.RegistrarAsync(lic.Id, codigoExterno, tenant.UserId, request, ct);
            return Ok(ApiResponse<DecisionDto>.Ok(dto));
        }
        catch (DecisionService.DecisionValidationException ex)
        {
            var status = ex.ErrorCode == "VAL_001" ? 400 : 422;
            return StatusCode(status, ApiResponse<object>.Fail(
                ex.ErrorCode == "VAL_001" ? "Solicitud inválida" : "Decisión inválida",
                [new ErrorDetail { Code = ex.ErrorCode, Message = ex.Message }]));
        }
    }

    /// <summary>Estado vigente de la decisión para la ficha (lectura local).</summary>
    [HttpGet]
    [ProducesResponseType(typeof(ApiResponse<DecisionEstadoDto>), 200)]
    [ProducesResponseType(typeof(ApiResponse<object>), 404)]
    public async Task<ActionResult<ApiResponse<DecisionEstadoDto>>> Obtener(
        string codigoExterno, CancellationToken ct = default)
    {
        var lic = await licitacionService.ObtenerPorCodigoAsync(codigoExterno, ct);
        if (lic == null)
            return NotFound(ApiResponse<object>.Fail(
                "Licitación no encontrada",
                [new ErrorDetail { Code = "LIC_001", Message = $"Licitación {codigoExterno} no encontrada" }]));

        var estado = await decisionService.ObtenerAsync(lic.Id, ct);
        return Ok(ApiResponse<DecisionEstadoDto>.Ok(estado));
    }
}
