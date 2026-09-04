using System.Data;
using Dapper;
using MPM.Core.Data;
using MPM.Modules.Administracion.Models;

namespace MPM.Modules.Administracion.Data;

/// <summary>
/// 037-C: Acceso a datos de costos LLM. Lee agregado diario desde
/// usp_LlmCostos_Resumen (V156) / v_llm_costos_diarios. Solo lectura, sin PII.
/// </summary>
public class AdminLlmCostosHandler(DbConnectionFactory dbFactory)
{
    private readonly DbConnectionFactory _dbFactory = dbFactory;

    public async Task<IEnumerable<LlmCostoDiaDto>> ResumenAsync(
        DateOnly? desde, DateOnly? hasta, CancellationToken ct = default)
    {
        await using var conn = _dbFactory.Create();
        // Función retorna TABLE(dia, provider, modelo, calls, tokens, costo).
        // Cast explícito ::date evita error 42883 cuando Dapper envía text.
        return await conn.QueryAsync<LlmCostoDiaDto>(
            sql: "SELECT * FROM usp_LlmCostos_Resumen(@p_desde::date, @p_hasta::date)",
            param: new
            {
                p_desde = desde?.ToString("yyyy-MM-dd"),
                p_hasta = hasta?.ToString("yyyy-MM-dd")
            },
            commandType: CommandType.Text);
    }
}
