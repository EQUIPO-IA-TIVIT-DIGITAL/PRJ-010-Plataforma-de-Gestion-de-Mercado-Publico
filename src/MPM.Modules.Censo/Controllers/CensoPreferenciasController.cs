using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MPM.Modules.Censo.Data;
using MPM.Modules.Censo.Models;
using MPM.Shared.Models;

namespace MPM.Modules.Censo.Controllers;

/// <summary>
/// Preferencias de usuario del match de capacidades (D7.12): toggle "Filtrar por país"
/// (OFF por defecto) + país. Actualización parcial (UPSERT).
/// </summary>
[ApiController]
[Route("api/v1/usuarios/me/preferencias-censo")]
[Authorize]
public class CensoPreferenciasController(CensoHandler handler) : ControllerBase
{
    private TenantContext? GetTenant() => HttpContext.Items["TenantContext"] as TenantContext;

    /// <summary>Preferencias actuales (defaults sin persistir si no hay fila).</summary>
    [HttpGet]
    [ProducesResponseType(typeof(ApiResponse<CensoPreferenciasDto>), 200)]
    public async Task<ActionResult<ApiResponse<CensoPreferenciasDto>>> Obtener(CancellationToken ct = default)
    {
        var tenant = GetTenant();
        if (tenant == null)
            return Unauthorized(ApiResponse<object>.Fail("No autenticado"));

        var prefs = await handler.PreferenciasObtenerAsync(tenant.UserId, ct);
        return Ok(ApiResponse<CensoPreferenciasDto>.Ok(prefs ?? new CensoPreferenciasDto()));
    }

    /// <summary>Actualización parcial (merge con los valores existentes; UPSERT).</summary>
    [HttpPut]
    [ProducesResponseType(typeof(ApiResponse<CensoPreferenciasDto>), 200)]
    [ProducesResponseType(typeof(ApiResponse<object>), 400)]
    public async Task<ActionResult<ApiResponse<CensoPreferenciasDto>>> Actualizar(
        [FromBody] CensoPreferenciasUpdateDto request, CancellationToken ct = default)
    {
        var tenant = GetTenant();
        if (tenant == null)
            return Unauthorized(ApiResponse<object>.Fail("No autenticado"));

        var existentes = await handler.PreferenciasObtenerAsync(tenant.UserId, ct) ?? new CensoPreferenciasDto();
        var filtrarPais = request.FiltrarPais ?? existentes.FiltrarPais;
        var pais = request.Pais ?? existentes.Pais;

        if (filtrarPais && string.IsNullOrWhiteSpace(pais))
            return BadRequest(ApiResponse<object>.Fail(
                "País requerido",
                [new ErrorDetail { Code = "VAL_001", Field = "pais", Message = "El país es obligatorio cuando el filtro está activo" }]));

        await handler.PreferenciasUpsertAsync(tenant.UserId, filtrarPais, pais, ct);
        return Ok(ApiResponse<CensoPreferenciasDto>.Ok(new CensoPreferenciasDto { FiltrarPais = filtrarPais, Pais = pais }));
    }
}
