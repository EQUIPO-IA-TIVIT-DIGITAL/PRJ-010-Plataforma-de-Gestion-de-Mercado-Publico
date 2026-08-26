using Dapper;
using MPM.Core.Data;
using MPM.Shared.Models;
using MPM.Shared.Services;
using Npgsql;
using System.Data;

namespace MPM.Modules.Licitaciones.Data;

public class CmResumenHandler(DbConnectionFactory dbFactory) : ICmResumenHandler
{
    private readonly DbConnectionFactory _dbFactory = dbFactory;

    public async Task UpsertCacheAsync(int anio, string rut, long amountClp, string payloadJson, CancellationToken ct = default)
    {
        await using var conn = _dbFactory.Create();
        // Usa SP si existe, fallback a INSERT directo para resiliencia en tests sin migración
        try
        {
            var p = new DynamicParameters();
            p.Add("p_anio", anio);
            p.Add("p_rut", rut);
            p.Add("p_amount_clp", amountClp);
            p.Add("p_payload_json", payloadJson, DbType.String);
            await conn.ExecuteAsync(new CommandDefinition(
                "CALL usp_CmResumenApi_Upsert(@p_anio, @p_rut, @p_amount_clp, @p_payload_json::jsonb)",
                p, cancellationToken: ct));
            return;
        }
        catch (Npgsql.PostgresException ex) when (ex.SqlState == "42883") // undefined_function
        {
            // SP aún no aplicada — fallback directo
        }

        await conn.ExecuteAsync(new CommandDefinition(
            """
            INSERT INTO cm_resumen_api_cache (anio, rut, amount_clp, payload_json, actualizado_at)
            VALUES (@anio, @rut, @amountClp, @payloadJson::jsonb, NOW())
            ON CONFLICT (anio, rut) DO UPDATE SET
              amount_clp = EXCLUDED.amount_clp,
              payload_json = EXCLUDED.payload_json,
              actualizado_at = NOW()
            """,
            new { anio = (short)anio, rut, amountClp, payloadJson },
            cancellationToken: ct));
    }

    public async Task<CmResumenCacheDto?> ObtenerPorAnioAsync(int anio, CancellationToken ct = default)
    {
        try
        {
            await using var conn = _dbFactory.Create();
            return await conn.QuerySingleOrDefaultAsync<CmResumenCacheDto>(new CommandDefinition(
                "SELECT anio, rut, amount_clp AS AmountClp, payload_json::text AS PayloadJson, actualizado_at AS ActualizadoAt FROM cm_resumen_api_cache WHERE anio = @anio",
                new { anio = (short)anio },
                cancellationToken: ct));
        }
        catch (PostgresException ex) when (ex.SqlState == "42P01") { return null; }
    }

    public async Task<CmResumenCacheDto?> ObtenerPorAnioAsync(string rut, int anio, CancellationToken ct = default)
    {
        try
        {
            await using var conn = _dbFactory.Create();
            return await conn.QuerySingleOrDefaultAsync<CmResumenCacheDto>(new CommandDefinition(
                "SELECT anio, rut, amount_clp AS AmountClp, payload_json::text AS PayloadJson, actualizado_at AS ActualizadoAt FROM cm_resumen_api_cache WHERE anio = @anio AND rut = @rut",
                new { anio = (short)anio, rut },
                cancellationToken: ct));
        }
        catch (PostgresException ex) when (ex.SqlState == "42P01") { return null; }
    }

    public async Task<IReadOnlyList<CmResumenCacheDto>> ObtenerRangoAsync(string rut, int anioDesde, int anioHasta, CancellationToken ct = default)
    {
        try
        {
            await using var conn = _dbFactory.Create();
            var rows = await conn.QueryAsync<CmResumenCacheDto>(new CommandDefinition(
                "SELECT anio, rut, amount_clp AS AmountClp, payload_json::text AS PayloadJson, actualizado_at AS ActualizadoAt FROM cm_resumen_api_cache WHERE rut = @rut AND anio BETWEEN @desde AND @hasta ORDER BY anio",
                new { rut, desde = (short)anioDesde, hasta = (short)anioHasta },
                cancellationToken: ct));
            return rows.ToList();
        }
        catch (PostgresException ex) when (ex.SqlState == "42P01") { return new List<CmResumenCacheDto>(); }
    }

    public async Task<long> ObtenerMontoAnualAsync(string rut, int anio, CancellationToken ct = default)
    {
        var row = await ObtenerPorAnioAsync(rut, anio, ct);
        return row?.AmountClp ?? 0L;
    }

    public Task<IReadOnlyList<CmResumenCacheDto>> ObtenerResumenAnualAsync(string rut, int anioDesde, int anioHasta, CancellationToken ct = default)
        => ObtenerRangoAsync(rut, anioDesde, anioHasta, ct);
}
