using Dapper;
using MPM.Core.Data;
using MPM.Modules.Catalogo.Models;
using System.Data;
using System.Threading;
using System.Threading.Tasks;

namespace MPM.Modules.Catalogo.Data;

public class CatalogoHandler(DbConnectionFactory dbFactory) : ICatalogoHandler
{
    private readonly DbConnectionFactory _dbFactory = dbFactory;

    public async Task<System.Collections.Generic.List<EstadoItemDto>> GetEstadosAsync(CancellationToken ct = default)
    {
        await using var conn = _dbFactory.Create();
        var result = await conn.QueryAsync<EstadoItemDto>(
            sql: CatalogoStoredProcedures.Estados,
            commandType: CommandType.Text);
        return result.ToList();
    }

    public async Task<System.Collections.Generic.List<TipoLicitacionItemDto>> GetTiposLicitacionAsync(CancellationToken ct = default)
    {
        await using var conn = _dbFactory.Create();
        var result = await conn.QueryAsync<TipoLicitacionItemDto>(
            sql: CatalogoStoredProcedures.TiposLicitacion,
            commandType: CommandType.Text);
        return result.ToList();
    }

    public async Task<System.Collections.Generic.List<MonedaItemDto>> GetMonedasAsync(CancellationToken ct = default)
    {
        await using var conn = _dbFactory.Create();
        var result = await conn.QueryAsync<MonedaItemDto>(
            sql: CatalogoStoredProcedures.Monedas,
            commandType: CommandType.Text);
        return result.ToList();
    }

    public async Task<CatalogosResponseDto> GetAllAsync(CancellationToken ct = default)
    {
        await using var conn = _dbFactory.Create();
        var estados = await conn.QueryAsync<EstadoItemDto>(
            sql: CatalogoStoredProcedures.Estados, commandType: CommandType.Text);
        var tipos = await conn.QueryAsync<TipoLicitacionItemDto>(
            sql: CatalogoStoredProcedures.TiposLicitacion, commandType: CommandType.Text);
        var monedas = await conn.QueryAsync<MonedaItemDto>(
            sql: CatalogoStoredProcedures.Monedas, commandType: CommandType.Text);

        return new CatalogosResponseDto
        {
            EstadosLicitacion = estados.ToList(),
            TiposLicitacion = tipos.ToList(),
            Monedas = monedas.ToList()
        };
    }
}