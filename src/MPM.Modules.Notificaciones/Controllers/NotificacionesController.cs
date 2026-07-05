using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MPM.Modules.Notificaciones.Models;
using MPM.Modules.Notificaciones.Services;
using MPM.Shared.Models;

namespace MPM.Modules.Notificaciones.Controllers;

[ApiController]
[Route("api/v1/notificaciones")]
[Authorize]
public class NotificacionesController(NotificacionesService service) : ControllerBase
{
    private readonly NotificacionesService _service = service;

    private TenantContext? GetTenant()
    {
        return HttpContext.Items["TenantContext"] as TenantContext;
    }

    [HttpGet]
    [ProducesResponseType(typeof(ApiResponse<PaginatedResult<NotificacionItemDto>>), 200)]
    public async Task<ActionResult<ApiResponse<PaginatedResult<NotificacionItemDto>>>> Listar(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        [FromQuery] bool soloNoLeidas = false)
    {
        var tenant = GetTenant();
        if (tenant == null) return Unauthorized(ApiResponse<object>.Fail("No autenticado"));

        var result = await _service.ListarAsync(tenant.UserId, page, pageSize, soloNoLeidas);
        return Ok(ApiResponse<PaginatedResult<NotificacionItemDto>>.Ok(result));
    }

    [HttpGet("no-leidas/count")]
    [ProducesResponseType(typeof(ApiResponse<NotificacionesCountDto>), 200)]
    public async Task<ActionResult<ApiResponse<NotificacionesCountDto>>> ContarNoLeidas()
    {
        var tenant = GetTenant();
        if (tenant == null) return Unauthorized(ApiResponse<object>.Fail("No autenticado"));

        var count = await _service.ContarNoLeidasAsync(tenant.UserId);
        return Ok(ApiResponse<NotificacionesCountDto>.Ok(new NotificacionesCountDto { Count = count }));
    }

    [HttpPut("{id:long}/leer")]
    [ProducesResponseType(typeof(ApiResponse<object>), 200)]
    [ProducesResponseType(typeof(ApiResponse<object>), 404)]
    public async Task<ActionResult<ApiResponse<object>>> MarcarLeida(long id)
    {
        var tenant = GetTenant();
        if (tenant == null) return Unauthorized(ApiResponse<object>.Fail("No autenticado"));

        var error = await _service.MarcarLeidaAsync(id, tenant.UserId);
        if (error != null)
            return NotFound(ApiResponse<object>.Fail(error));

        return Ok(ApiResponse<object>.Ok(new { }));
    }

    [HttpPut("leer-todas")]
    [ProducesResponseType(typeof(ApiResponse<object>), 200)]
    [ProducesResponseType(typeof(ApiResponse<object>), 400)]
    public async Task<ActionResult<ApiResponse<object>>> MarcarTodasLeidas()
    {
        var tenant = GetTenant();
        if (tenant == null) return Unauthorized(ApiResponse<object>.Fail("No autenticado"));

        var (count, error) = await _service.MarcarTodasLeidasAsync(tenant.UserId);
        if (error != null)
            return BadRequest(ApiResponse<object>.Fail(error));

        return Ok(ApiResponse<object>.Ok(new { marcadas = count }));
    }

    [HttpDelete("{id:long}")]
    [ProducesResponseType(typeof(ApiResponse<object>), 200)]
    [ProducesResponseType(typeof(ApiResponse<object>), 404)]
    public async Task<ActionResult<ApiResponse<object>>> Eliminar(long id)
    {
        var tenant = GetTenant();
        if (tenant == null) return Unauthorized(ApiResponse<object>.Fail("No autenticado"));

        var error = await _service.EliminarAsync(id, tenant.UserId);
        if (error != null)
            return NotFound(ApiResponse<object>.Fail(error));

        return Ok(ApiResponse<object>.Ok(new { }));
    }

    [HttpDelete]
    [ProducesResponseType(typeof(ApiResponse<object>), 200)]
    [ProducesResponseType(typeof(ApiResponse<object>), 400)]
    public async Task<ActionResult<ApiResponse<object>>> EliminarTodas()
    {
        var tenant = GetTenant();
        if (tenant == null) return Unauthorized(ApiResponse<object>.Fail("No autenticado"));

        var (count, error) = await _service.EliminarTodasAsync(tenant.UserId);
        if (error != null)
            return BadRequest(ApiResponse<object>.Fail(error));

        return Ok(ApiResponse<object>.Ok(new { eliminadas = count }));
    }
}
