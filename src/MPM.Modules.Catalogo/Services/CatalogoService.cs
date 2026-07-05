using MPM.Modules.Catalogo.Data;
using MPM.Modules.Catalogo.Models;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace MPM.Modules.Catalogo.Services;

public class CatalogoService
{
    private readonly ICatalogoHandler _catalogoHandler;

    public CatalogoService(ICatalogoHandler catalogoHandler)
    {
        _catalogoHandler = catalogoHandler;
    }

    public async Task<List<EstadoItemDto>> GetEstadosAsync(CancellationToken ct = default)
    {
        return await _catalogoHandler.GetEstadosAsync(ct);
    }

    public async Task<List<TipoLicitacionItemDto>> GetTiposLicitacionAsync(CancellationToken ct = default)
    {
        return await _catalogoHandler.GetTiposLicitacionAsync(ct);
    }

    public async Task<List<MonedaItemDto>> GetMonedasAsync(CancellationToken ct = default)
    {
        return await _catalogoHandler.GetMonedasAsync(ct);
    }

    public async Task<CatalogosResponseDto> GetAllAsync(CancellationToken ct = default)
    {
        return await _catalogoHandler.GetAllAsync(ct);
    }
}