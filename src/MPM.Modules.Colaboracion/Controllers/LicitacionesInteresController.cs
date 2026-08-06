using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MPM.Modules.Colaboracion.Models;
using MPM.Modules.Colaboracion.Services;
using MPM.Shared.Models;

namespace MPM.Modules.Colaboracion.Controllers;

[ApiController]
[Route("api/v1/licitaciones")]
public class LicitacionesInteresController(LicitacionesInteresService service) : ControllerBase
{
    private TenantContext? GetTenant() => HttpContext.Items["TenantContext"] as TenantContext;

    // [Authorize] en las mutaciones -- mismo patrón que /{codigoExterno}/seguir en
    // LicitacionController (la acción análoga de "marcar" más cercana en el repo).
    [HttpPost("{licitacionId}/interes")]
    [Authorize]
    public async Task<ActionResult<ApiResponse<LicitacionInteresDto>>> Marcar(long licitacionId, CancellationToken ct)
    {
        var tenant = GetTenant();
        if (tenant == null) return Unauthorized(ApiResponse<object>.Fail("No autenticado"));

        var dto = await service.MarcarInteresAsync(licitacionId, tenant.UserId, ct);
        return Ok(ApiResponse<LicitacionInteresDto>.Ok(dto));
    }

    [HttpGet("{licitacionId}/interes")]
    [Authorize]
    public async Task<ActionResult<ApiResponse<LicitacionInteresDto>>> Obtener(long licitacionId, CancellationToken ct)
    {
        var dto = await service.ObtenerAsync(licitacionId, ct);
        if (dto == null)
            return NotFound(ApiResponse<LicitacionInteresDto>.Fail(
                "COL_001:Esta licitación no ha sido marcada de interés"));

        return Ok(ApiResponse<LicitacionInteresDto>.Ok(dto));
    }

    [HttpPatch("{licitacionId}/interes/vincular")]
    [Authorize]
    public async Task<ActionResult<ApiResponse<LicitacionInteresDto>>> Vincular(
        long licitacionId, [FromBody] VincularInteresRequest request, CancellationToken ct)
    {
        var dto = await service.VincularAsync(licitacionId, request, ct);
        if (dto == null)
            return NotFound(ApiResponse<LicitacionInteresDto>.Fail(
                "COL_001:Esta licitación no ha sido marcada de interés"));

        return Ok(ApiResponse<LicitacionInteresDto>.Ok(dto));
    }

    [HttpGet("interes")]
    [Authorize]
    public async Task<ActionResult<ApiResponse<List<LicitacionInteresListItemDto>>>> Listar(CancellationToken ct)
    {
        var items = await service.ListarAsync(ct);
        return Ok(ApiResponse<List<LicitacionInteresListItemDto>>.Ok(items));
    }
}
