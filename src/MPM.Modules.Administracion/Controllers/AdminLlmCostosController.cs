using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using MPM.Modules.Administracion.Models;
using MPM.Modules.Administracion.Services;
using MPM.Shared.Models;

namespace MPM.Modules.Administracion.Controllers;

/// <summary>
/// 037-C: Endpoint admin de costos LLM (SuperAdmin only).
/// GET /api/v1/admin/llm-costos?desde=YYYY-MM-DD&hasta=YYYY-MM-DD
/// Retorna agregado diario por provider/modelo sin exponer prompts (OBS-R005).
/// Usa usp_LlmCostos_Resumen / v_llm_costos_diarios (V156).
/// </summary>
[ApiController]
[Route("api/v1/admin/llm-costos")]
[Authorize(Roles = "SuperAdmin")]
public class AdminLlmCostosController(AdminLlmCostosService service, ILogger<AdminLlmCostosController> logger)
    : ControllerBase
{
    /// <summary>
    /// Agregado diario de costos LLM por provider y modelo.
    /// Parámetros opcionales; por defecto últimos 30 días. Solo SuperAdmin (403 si no).
    /// </summary>
    [HttpGet]
    [ProducesResponseType(typeof(ApiResponse<IEnumerable<LlmCostoDiaDto>>), 200)]
    [ProducesResponseType(typeof(ApiResponse<object>), 401)]
    [ProducesResponseType(typeof(ApiResponse<object>), 403)]
    public async Task<ActionResult<ApiResponse<IEnumerable<LlmCostoDiaDto>>>> Resumen(
        [FromQuery] string? desde = null,
        [FromQuery] string? hasta = null,
        CancellationToken ct = default)
    {
        try
        {
            DateOnly? desdeDate = null;
            DateOnly? hastaDate = null;

            if (!string.IsNullOrWhiteSpace(desde))
            {
                if (!DateOnly.TryParse(desde, out var d))
                    return BadRequest(ApiResponse<object>.Fail($"Parámetro 'desde' inválido. Use YYYY-MM-DD. Valor recibido: {desde}"));
                desdeDate = d;
            }
            if (!string.IsNullOrWhiteSpace(hasta))
            {
                if (!DateOnly.TryParse(hasta, out var h))
                    return BadRequest(ApiResponse<object>.Fail($"Parámetro 'hasta' inválido. Use YYYY-MM-DD. Valor recibido: {hasta}"));
                hastaDate = h;
            }

            var data = await service.ResumenAsync(desdeDate, hastaDate, ct);
            var list = data.ToList();
            logger.LogDebug("AdminLlmCostos: desde {Desde} hasta {Hasta} -> {Count} filas", desdeDate, hastaDate, list.Count);
            return Ok(ApiResponse<IEnumerable<LlmCostoDiaDto>>.Ok(list));
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(ApiResponse<object>.Fail(ex.Message));
        }
    }
}
