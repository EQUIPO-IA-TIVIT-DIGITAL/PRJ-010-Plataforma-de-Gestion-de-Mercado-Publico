using System.Data;
using Dapper;
using MPM.Core.Data;

namespace MPM.Modules.Licitaciones.Data;

public class ExtraccionLogHandler(DbConnectionFactory dbFactory)
{
    private readonly DbConnectionFactory _dbFactory = dbFactory;

    public virtual async Task<long> RegistrarAsync(
        long licitacionId, string metodo, string estado, int documentosObtenidos,
        bool actaObtenida, bool esFallback, string? error, long duracionMs,
        CancellationToken ct = default)
    {
        await using var conn = _dbFactory.Create();
        var result = await conn.QueryAsync<RegistrarResult>(
            ExtraccionStoredProcedures.Registrar,
            new
            {
                p_licitacion_id = licitacionId,
                p_metodo = metodo,
                p_estado = estado,
                p_documentos_obtenidos = documentosObtenidos,
                p_acta_obtenida = actaObtenida,
                p_es_fallback = esFallback,
                p_error = error,
                p_duracion_ms = duracionMs,
            },
            commandType: CommandType.Text);

        return result.FirstOrDefault()?.p_id ?? 0;
    }

    public async Task<IEnumerable<ResumenPeriodoResult>> ResumenPeriodoAsync(
        DateTime desde, DateTime hasta, CancellationToken ct = default)
    {
        await using var conn = _dbFactory.Create();
        return await conn.QueryAsync<ResumenPeriodoResult>(
            ExtraccionStoredProcedures.ResumenPeriodo,
            new { p_desde = desde, p_hasta = hasta },
            commandType: CommandType.Text);
    }

    public async Task<bool> ExistePorLicitacionAsync(long licitacionId, CancellationToken ct = default)
    {
        await using var conn = _dbFactory.Create();
        var result = await conn.QueryAsync<ExisteResult>(
            ExtraccionStoredProcedures.ExistePorLicitacion,
            new { p_licitacion_id = licitacionId },
            commandType: CommandType.Text);

        return result.FirstOrDefault()?.p_existe ?? false;
    }

    public async Task<long> RegistrarAdjuntoDirectoAsync(
        long licitacionId, string tipo, string nombreArchivo, string rutaStorage,
        long? tamanioBytes, string? mimeType, bool esActa, CancellationToken ct = default)
    {
        await using var conn = _dbFactory.Create();
        var result = await conn.QueryAsync<RegistrarResult>(
            ExtraccionStoredProcedures.RegistrarAdjuntoDirecto,
            new
            {
                p_licitacion_id = licitacionId,
                p_tipo = tipo,
                p_nombre_archivo = nombreArchivo,
                p_ruta_storage = rutaStorage,
                p_tamanio_bytes = tamanioBytes,
                p_mime_type = mimeType,
                p_es_acta = esActa,
            },
            commandType: CommandType.Text);

        return result.FirstOrDefault()?.p_id ?? 0;
    }

    private class RegistrarResult
    {
        public long p_id { get; set; }
    }

    private class ExisteResult
    {
        public bool p_existe { get; set; }
    }

    public class ResumenPeriodoResult
    {
        public string p_metodo { get; set; } = "";
        public string p_estado { get; set; } = "";
        public long p_total { get; set; }
        public decimal? p_promedio_duracion_ms { get; set; }
    }
}
