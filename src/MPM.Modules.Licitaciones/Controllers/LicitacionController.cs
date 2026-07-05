using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MPM.Shared.Models;
using MPM.Modules.Licitaciones.Data;
using MPM.Modules.Licitaciones.Models;
using MPM.Modules.Licitaciones.Services;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace MPM.Modules.Licitaciones.Controllers;

[ApiController]
[Route("api/v1/licitaciones")]
public class LicitacionController(
    LicitacionService licitacionService,
    SeguimientoHandler seguimientoHandler) : ControllerBase
{
    private TenantContext? GetTenant() => HttpContext.Items["TenantContext"] as TenantContext;
    [HttpGet]
    public async Task<ActionResult<ApiResponse<PaginatedResult<LicitacionResumenDto>>>> Listar(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        [FromQuery] string? search = null,
        [FromQuery] short? estado = null,
        [FromQuery] string? tipo = null,
        [FromQuery] string? organismo = null,
        [FromQuery] string? fechaDesde = null,
        [FromQuery] string? fechaHasta = null,
        [FromQuery] string sortBy = "fecha_publicacion",
        [FromQuery] string sortDir = "desc",
        CancellationToken ct = default)
    {
        DateTime? fechaDesdeDt = null;
        DateTime? fechaHastaDt = null;

        if (!string.IsNullOrEmpty(fechaDesde) && DateTime.TryParse(fechaDesde, out var fd))
            fechaDesdeDt = fd;
        if (!string.IsNullOrEmpty(fechaHasta) && DateTime.TryParse(fechaHasta, out var fh))
            fechaHastaDt = fh;

        var (items, totalCount) = await licitacionService.ListarAsync(
            page, pageSize, search, estado, tipo, organismo,
            fechaDesdeDt, fechaHastaDt, sortBy, sortDir, ct);

        var paginatedResult = new PaginatedResult<LicitacionResumenDto>
        {
            Items = items,
            Page = page,
            PageSize = pageSize,
            TotalRecords = totalCount
        };

        return Ok(ApiResponse<PaginatedResult<LicitacionResumenDto>>.Ok(paginatedResult));
    }

    [HttpGet("{codigoExterno}")]
    public async Task<ActionResult<ApiResponse<LicitacionDetalleDto>>> ObtenerPorCodigo(
        string codigoExterno, CancellationToken ct = default)
    {
        var licitacion = await licitacionService.ObtenerPorCodigoAsync(codigoExterno, ct);
        if (licitacion == null)
            return NotFound(ApiResponse<LicitacionDetalleDto>.Fail(
                "Licitación no encontrada",
                [new ErrorDetail { Code = "LIC_001", Message = $"Licitación {codigoExterno} no encontrada" }]));

        return Ok(ApiResponse<LicitacionDetalleDto>.Ok(licitacion));
    }

    [HttpGet("buscar")]
    public async Task<ActionResult<ApiResponse<List<LicitacionSearchResult>>>> Buscar(
        [FromQuery] string q, [FromQuery] int limit = 10, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(q) || q.Length < 3)
            return BadRequest(ApiResponse<List<LicitacionSearchResult>>.Fail(
                "Búsqueda debe tener al menos 3 caracteres",
                [new ErrorDetail { Code = "VAL_001", Field = "q", Message = "Mínimo 3 caracteres" }]));

        var results = await licitacionService.BuscarAsync(q, limit, ct);
        return Ok(ApiResponse<List<LicitacionSearchResult>>.Ok(results));
    }

    [HttpPost("sync")]
    public async Task<ActionResult<ApiResponse<SyncStatusDto>>> ForzarSync(
        [FromQuery] DateTime? desde = null,
        CancellationToken ct = default)
    {
        var syncStatus = await licitacionService.ForzarSyncAsync(desde, ct);
        return Ok(ApiResponse<SyncStatusDto>.Ok(syncStatus));
    }

    [HttpPost("{codigoExterno}/seguir")]
    [Authorize]
    [ProducesResponseType(typeof(ApiResponse<SeguimientoToggleDto>), 200)]
    public async Task<ActionResult<ApiResponse<SeguimientoToggleDto>>> SeguirToggle(
        string codigoExterno, CancellationToken ct = default)
    {
        var tenant = GetTenant();
        if (tenant == null) return Unauthorized(ApiResponse<object>.Fail("No autenticado"));

        var (accion, error) = await seguimientoHandler.SeguirToggleAsync(tenant.UserId, codigoExterno, ct);
        if (error != null) return BadRequest(ApiResponse<object>.Fail(error));

        return Ok(ApiResponse<SeguimientoToggleDto>.Ok(new SeguimientoToggleDto
        {
            CodigoExterno = codigoExterno,
            Accion = accion ?? "no_seguida",
        }));
    }

    [HttpGet("{codigoExterno}/seguida")]
    [Authorize]
    [ProducesResponseType(typeof(ApiResponse<EsSeguidaDto>), 200)]
    public async Task<ActionResult<ApiResponse<EsSeguidaDto>>> EsSeguida(
        string codigoExterno, CancellationToken ct = default)
    {
        var tenant = GetTenant();
        if (tenant == null) return Unauthorized(ApiResponse<object>.Fail("No autenticado"));

        var esSeguida = await seguimientoHandler.EsSeguidaAsync(tenant.UserId, codigoExterno, ct);
        return Ok(ApiResponse<EsSeguidaDto>.Ok(new EsSeguidaDto { EsSeguida = esSeguida }));
    }

    [HttpGet("seguidas")]
    [Authorize]
    [ProducesResponseType(typeof(ApiResponse<List<LicitacionSeguidaDto>>), 200)]
    public async Task<ActionResult<ApiResponse<List<LicitacionSeguidaDto>>>> ObtenerSeguidas(
        CancellationToken ct = default)
    {
        var tenant = GetTenant();
        if (tenant == null) return Unauthorized(ApiResponse<object>.Fail("No autenticado"));

        var items = await seguimientoHandler.ObtenerSeguidasAsync(tenant.UserId, ct);
        return Ok(ApiResponse<List<LicitacionSeguidaDto>>.Ok(items.ToList()));
    }

    [HttpGet("buscar-natural")]
    [ProducesResponseType(typeof(ApiResponse<PaginatedResult<LicitacionNaturalSearchResult>>), 200)]
    [ProducesResponseType(typeof(ApiResponse<object>), 400)]
    public async Task<ActionResult<ApiResponse<PaginatedResult<LicitacionNaturalSearchResult>>>> BuscarNatural(
        [FromQuery] string q,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        [FromQuery] short? estado = null,
        CancellationToken ct = default)
    {
        var (result, error) = await licitacionService.BuscarNaturalAsync(q, page, pageSize, estado, ct);
        if (error != null)
            return BadRequest(ApiResponse<object>.Fail(error));

        return Ok(ApiResponse<PaginatedResult<LicitacionNaturalSearchResult>>.Ok(result!));
    }
}
