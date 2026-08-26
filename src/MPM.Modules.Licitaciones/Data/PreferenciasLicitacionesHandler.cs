using System.Data;
using Dapper;
using MPM.Core.Data;
using MPM.Modules.Licitaciones.Models;

namespace MPM.Modules.Licitaciones.Data;

/// <summary>
/// Handler Dapper para preferencias_usuario — patron identico a CensoHandler (V143) y LicitacionHandler.
/// </summary>
public class PreferenciasLicitacionesHandler(DbConnectionFactory dbFactory)
{
    private readonly DbConnectionFactory _dbFactory = dbFactory;

    /// <summary>
    /// Obtiene la preferencia del usuario. Retorna null si no hay fila (patron censo: sin fila => defaults).
    /// </summary>
    public virtual async Task<PreferenciasLicitacionesDto?> PreferenciasObtenerAsync(string userId, CancellationToken ct = default)
    {
        await using var conn = _dbFactory.Create();
        var rows = await conn.QueryAsync<PreferenciasRow>(
            PreferenciasLicitacionesStoredProcedures.Obtener,
            new { p_user_id = userId },
            commandType: CommandType.Text);

        var row = rows.FirstOrDefault();
        if (row == null) return null;

        return new PreferenciasLicitacionesDto { MontoMinimo = row.monto_minimo };
    }

    /// <summary>
    /// Upsert idempotente (INSERT ... ON CONFLICT DO UPDATE). p_monto_minimo NULL borra la preferencia.
    /// Lanza InvalidOperationException si el SP retorna VAL_001 / SYS_001 en p_error_msg.
    /// </summary>
    public virtual async Task PreferenciasUpsertAsync(string userId, decimal? montoMinimo, CancellationToken ct = default)
    {
        await using var conn = _dbFactory.Create();
        var p = new DynamicParameters();
        p.Add("p_user_id", userId, DbType.String);
        p.Add("p_monto_minimo", montoMinimo, DbType.Decimal);
        p.Add("p_error_msg", "", DbType.String, ParameterDirection.InputOutput);

        await conn.ExecuteAsync(PreferenciasLicitacionesStoredProcedures.Upsert, p, commandType: CommandType.Text);

        var error = p.Get<string?>("p_error_msg");
        if (!string.IsNullOrWhiteSpace(error))
        {
            // El controller valida <0 antes de llegar acá; este throw es salvaguarda para CHECK/SP y para tests de integración.
            throw new InvalidOperationException(error);
        }
    }

    private class PreferenciasRow
    {
        public decimal? monto_minimo { get; set; }
        public DateTime? updated_at { get; set; }
        // user_id viene en el result set pero no se necesita aquí — se ignora por Dapper.
    }
}
