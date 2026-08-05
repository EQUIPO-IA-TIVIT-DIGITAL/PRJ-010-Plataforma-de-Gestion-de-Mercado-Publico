using Microsoft.AspNetCore.Mvc;
using MPM.Shared.Models;
using MPM.Modules.Catalogo.Models;
using MPM.Modules.Catalogo.Services;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace MPM.Modules.Catalogo.Controllers;

[ApiController]
[Route("api/v1/catalogos")]
public class CatalogoController(CatalogoService catalogoService) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<ApiResponse<CatalogosResponseDto>>> GetAll(CancellationToken ct)
    {
        var catalogos = await catalogoService.GetAllAsync(ct);
        return Ok(ApiResponse<CatalogosResponseDto>.Ok(catalogos));
    }

    [HttpGet("estados-licitacion")]
    public async Task<ActionResult<ApiResponse<List<EstadoItemDto>>>> GetEstados(CancellationToken ct)
    {
        var estados = await catalogoService.GetEstadosAsync(ct);
        return Ok(ApiResponse<List<EstadoItemDto>>.Ok(estados));
    }

    [HttpGet("tipos-licitacion")]
    public async Task<ActionResult<ApiResponse<List<TipoLicitacionItemDto>>>> GetTiposLicitacion(CancellationToken ct)
    {
        var tipos = await catalogoService.GetTiposLicitacionAsync(ct);
        return Ok(ApiResponse<List<TipoLicitacionItemDto>>.Ok(tipos));
    }

    [HttpGet("monedas")]
    public async Task<ActionResult<ApiResponse<List<MonedaItemDto>>>> GetMonedas(CancellationToken ct)
    {
        var monedas = await catalogoService.GetMonedasAsync(ct);
        return Ok(ApiResponse<List<MonedaItemDto>>.Ok(monedas));
    }

    [HttpGet("areas-negocio")]
    public async Task<ActionResult<ApiResponse<List<AreaNegocioItemDto>>>> GetAreasNegocio(CancellationToken ct)
    {
        var areas = await catalogoService.GetAreasNegocioAsync(ct);
        return Ok(ApiResponse<List<AreaNegocioItemDto>>.Ok(areas));
    }
}