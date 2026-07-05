using MPM.Modules.Catalogo.Models;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace MPM.Modules.Catalogo.Data;

public interface ICatalogoHandler
{
    Task<List<EstadoItemDto>> GetEstadosAsync(CancellationToken ct = default);
    Task<List<TipoLicitacionItemDto>> GetTiposLicitacionAsync(CancellationToken ct = default);
    Task<List<MonedaItemDto>> GetMonedasAsync(CancellationToken ct = default);
    Task<CatalogosResponseDto> GetAllAsync(CancellationToken ct = default);
}
