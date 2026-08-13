using Dapper;
using MPM.Core.Data;
using System.Data;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace MPM.Api.Services;

/// <summary>
/// Backfill one-shot de areas_negocio por licitación (V136) — ejecutado como Cloud Run
/// Job (WORKER_MODE=backfill-areas), NO en el arranque del servicio web.
///
/// Estrategia (v3, 2026-08-13): el JOIN por lote contra las 50 keywords tardaba ~2min por
/// lote (el planner no usaba el GIN de search_vector) — 128k filas pendientes = horas.
/// Ahora: (1) marca todas las pendientes como '{}' con un UPDATE simple y barato, y
/// (2) por CADA área (3 total), un UPDATE con to_tsquery compuesto (OR de sus keywords)
/// que SÍ usa el índice GIN de search_vector (filtro directo, no JOIN). Total: 4 UPDATEs.
///
/// Idempotente: solo procesa filas con area_codigos IS NULL; un arranque interrumpido
/// retoma marcando '{}' y re-aplicando las áreas (los códigos ya presentes no se duplican
/// porque el UPDATE por área solo agrega si el área matchea y el código no está).
/// </summary>
public class AreasBackfillService(DbConnectionFactory dbFactory, ILogger<AreasBackfillService> logger)
{
    private readonly DbConnectionFactory _dbFactory = dbFactory;
    private readonly ILogger<AreasBackfillService> _logger = logger;

    public async Task EjecutarAsync(CancellationToken ct = default)
    {
        // La conexión a Cloud SQL privada es intermitente desde Cloud Run Jobs (visto en prod
        // 2026-08-13: la 2ª y 3ª ejecución fallaron con "Failed to connect ... The operation
        // has timed out" mientras el sync-job conectaba bien). Se reintenta con backoff corto.
        await using var conn = _dbFactory.Create();
        Exception? ultimoError = null;
        for (var intento = 1; intento <= 5; intento++)
        {
            try
            {
                await conn.OpenAsync(ct);
                ultimoError = null;
                break;
            }
            catch (Exception ex) when (intento < 5)
            {
                ultimoError = ex;
                _logger.LogWarning("Backfill areas: conexión a BD falló (intento {Intento}/5): {Error}",
                    intento, ex.Message.Split('\n')[0]);
                await Task.Delay(TimeSpan.FromSeconds(10 * intento), ct);
            }
        }
        if (ultimoError != null)
            throw ultimoError;

        var pendientes = await conn.ExecuteScalarAsync<long>(
            "SELECT COUNT(*) FROM licitaciones WHERE deleted_at IS NULL AND area_codigos IS NULL",
            commandTimeout: 300);
        _logger.LogInformation("Backfill areas: {Pendientes} licitaciones pendientes de clasificar", pendientes);
        if (pendientes == 0)
        {
            _logger.LogInformation("Backfill areas: nada pendiente, terminando.");
            return;
        }

        // Paso 1: las pendientes quedan '{}' (sin clasificar por defecto). UPDATE simple,
        // sin JOIN — barato incluso sobre 187k filas.
        var marcadas = await conn.ExecuteAsync(
            "UPDATE licitaciones SET area_codigos = '{}' WHERE deleted_at IS NULL AND area_codigos IS NULL",
            commandTimeout: 600);
        _logger.LogInformation("Backfill areas paso 1: {Marcadas} marcadas como sin clasificar", marcadas);

        // Paso 2: por cada área, un UPDATE dirigido con OR de keywords — el filtro
        // search_vector @@ to_tsquery(...) usa el índice GIN (a diferencia del JOIN).
        var areas = await conn.QueryAsync<(short Codigo, string Nombre, string OrKeywords)>("""
            SELECT codigo AS Codigo, nombre AS Nombre,
                   array_to_string(palabras_clave, ' | ') AS OrKeywords
            FROM areas_negocio
            ORDER BY codigo
            """, commandTimeout: 300);

        foreach (var area in areas)
        {
            if (string.IsNullOrWhiteSpace(area.OrKeywords)) continue;

            // to_tsquery con OR de keywords: las frases con espacios van entre comillas
            // dobles para que se parseen como frases, el resto como términos simples.
            var tsquery = ConstruirTsquery(area.OrKeywords);
            var actualizadas = await conn.ExecuteAsync("""
                UPDATE licitaciones
                SET area_codigos = CASE
                        WHEN area_codigos = '{}' THEN ARRAY[@codigo]
                        WHEN NOT (@codigo = ANY(area_codigos)) THEN area_codigos || @codigo
                        ELSE area_codigos
                    END
                WHERE deleted_at IS NULL
                  AND search_vector @@ to_tsquery('spanish', @tsquery)
                """, new { codigo = area.Codigo, tsquery }, commandTimeout: 900);
            _logger.LogInformation("Backfill areas área {Nombre} (codigo {Codigo}): {Actualizadas} filas",
                area.Nombre, area.Codigo, actualizadas);
        }

        // Paso 3: normalizar orden del array (1,2,3) para consistencia con el resto del sistema.
        await conn.ExecuteAsync("""
            UPDATE licitaciones
            SET area_codigos = ARRAY(SELECT unnest(area_codigos) ORDER BY 1)
            WHERE deleted_at IS NULL AND cardinality(area_codigos) > 1
            """, commandTimeout: 900);

        var restantes = await conn.ExecuteScalarAsync<long>(
            "SELECT COUNT(*) FROM licitaciones WHERE deleted_at IS NULL AND area_codigos IS NULL",
            commandTimeout: 300);
        _logger.LogInformation("Backfill areas completado. Restantes sin clasificar: {Restantes}", restantes);
    }

    /// <summary>Convierte "a | b | c con espacios | d" a "a | b | 'c con espacios' | d".</summary>
    private static string ConstruirTsquery(string orKeywords)
    {
        var partes = orKeywords.Split('|', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        var sb = new StringBuilder();
        foreach (var parte in partes)
        {
            if (sb.Length > 0) sb.Append(" | ");
            sb.Append(parte.Contains(' ') ? $"'{parte.Replace("'", "''")}'" : parte);
        }
        return sb.ToString();
    }
}
