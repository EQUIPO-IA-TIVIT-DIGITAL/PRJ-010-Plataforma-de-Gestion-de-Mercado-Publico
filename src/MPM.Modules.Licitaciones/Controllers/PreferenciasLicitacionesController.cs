using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MPM.Modules.Licitaciones.Data;
using MPM.Modules.Licitaciones.Models;
using MPM.Shared.Models;

namespace MPM.Modules.Licitaciones.Controllers;

/// <summary>
/// Preferencias de listado por usuario — monto mínimo por defecto (Feature B, Track 1).
/// Spec: docs/api-first/preferencias-usuario.md
/// Patron: replica exacta de CensoPreferenciasController (GET sin fila => defaults, PUT valida VAL_001, user_id siempre del JWT).
/// </summary>
[ApiController]
[Route("api/v1/usuarios/me/preferencias-licitaciones")]
[Authorize]
public class PreferenciasLicitacionesController(PreferenciasLicitacionesHandler handler) : ControllerBase
{
    private TenantContext? GetTenant() => HttpContext.Items["TenantContext"] as TenantContext;

    /// <summary>Obtiene la preferencia del usuario autenticado. Sin fila => { montoMinimo: null } (no 404).</summary>
    [HttpGet]
    [ProducesResponseType(typeof(ApiResponse<PreferenciasLicitacionesDto>), 200)]
    public async Task<ActionResult<ApiResponse<PreferenciasLicitacionesDto>>> Obtener(CancellationToken ct = default)
    {
        var tenant = GetTenant();
        if (tenant == null || string.IsNullOrWhiteSpace(tenant.UserId))
            return Unauthorized(ApiResponse<object>.Fail("No autenticado"));

        var dto = await handler.PreferenciasObtenerAsync(tenant.UserId, ct);
        // Patron censo: sin fila => defaults vacios (montoMinimo null)
        dto ??= new PreferenciasLicitacionesDto { MontoMinimo = null };
        return Ok(ApiResponse<PreferenciasLicitacionesDto>.Ok(dto));
    }

    /// <summary>Upsert idempotente de la preferencia. montoMinimo null => borra (PREF-R003). Valida VAL_001 si negativo.</summary>
    [HttpPut]
    [ProducesResponseType(typeof(ApiResponse<PreferenciasLicitacionesDto>), 200)]
    [ProducesResponseType(typeof(ApiResponse<object>), 400)]
    public async Task<ActionResult<ApiResponse<PreferenciasLicitacionesDto>>> Actualizar(
        [FromBody] PreferenciasLicitacionesUpdateDto request, CancellationToken ct = default)
    {
        var tenant = GetTenant();
        if (tenant == null || string.IsNullOrWhiteSpace(tenant.UserId))
            return Unauthorized(ApiResponse<object>.Fail("No autenticado"));

        if (request == null)
            return BadRequest(ApiResponse<object>.Fail(
                "Body requerido",
                [new ErrorDetail { Code = "VAL_001", Field = "montoMinimo", Message = "Body requerido" }]));

        // VAL_001: negativo o fuera de rango (spec §5 y §9). El CHECK de la tabla es segunda barrera; el SP también valida.
        if (request.MontoMinimo.HasValue && request.MontoMinimo.Value < 0)
            return BadRequest(ApiResponse<object>.Fail(
                "Monto inválido",
                [new ErrorDetail { Code = "VAL_001", Field = "montoMinimo", Message = "montoMinimo no puede ser negativo" }]));

        if (request.MontoMinimo.HasValue && request.MontoMinimo.Value > 999999999999.99m)
            return BadRequest(ApiResponse<object>.Fail(
                "Monto inválido",
                [new ErrorDetail { Code = "VAL_001", Field = "montoMinimo", Message = "montoMinimo fuera de rango (máx 999999999999.99)" }]));

        try
        {
            // user_id SIEMPRE del JWT (PREF-R010) — nunca del body/ruta.
            await handler.PreferenciasUpsertAsync(tenant.UserId, request.MontoMinimo, ct);
        }
        catch (InvalidOperationException ex) when (ex.Message.StartsWith("VAL_001"))
        {
            return BadRequest(ApiResponse<object>.Fail(
                "Monto inválido",
                [new ErrorDetail { Code = "VAL_001", Field = "montoMinimo", Message = ex.Message }]));
        }

        // Echo del valor persistido (spec PUT 200). Re-leer no es necesario pero garantiza coherencia si SP normaliza.
        var persisted = await handler.PreferenciasObtenerAsync(tenant.UserId, ct)
                        ?? new PreferenciasLicitacionesDto { MontoMinimo = request.MontoMinimo };

        return Ok(ApiResponse<PreferenciasLicitacionesDto>.Ok(persisted));
    }
}
