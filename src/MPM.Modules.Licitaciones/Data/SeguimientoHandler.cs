using Dapper;
using MPM.Core.Data;
using MPM.Modules.Licitaciones.Models;
using Npgsql;
using System.Data;

namespace MPM.Modules.Licitaciones.Data;

public class SeguimientoHandler(DbConnectionFactory dbFactory)
{
    private readonly DbConnectionFactory _dbFactory = dbFactory;

    public async Task<(string? Accion, string? Error)> SeguirToggleAsync(
        string usuarioId, string codigoExterno, CancellationToken ct = default)
    {
        await using var conn = _dbFactory.Create();
        var result = await conn.QueryAsync<ToggleResult>(
            LicitacionStoredProcedures.SeguirToggle,
            new { p_usuario_id = usuarioId, p_codigo = codigoExterno },
            commandType: CommandType.Text);

        var row = result.FirstOrDefault();
        if (row == null) return (null, "SYS_001: Sin respuesta");
        return (row.p_accion, row.p_error_msg);
    }

    public async Task<bool> EsSeguidaAsync(
        string usuarioId, string codigoExterno, CancellationToken ct = default)
    {
        await using var conn = _dbFactory.Create();
        var result = await conn.QueryAsync<EsSeguidaResult>(
            LicitacionStoredProcedures.EsSeguida,
            new { p_usuario_id = usuarioId, p_codigo = codigoExterno },
            commandType: CommandType.Text);

        return result.FirstOrDefault()?.p_es_seguida ?? false;
    }

    public async Task<IEnumerable<LicitacionParaMonitorDto>> ObtenerParaMonitorAsync(
        int[] estados, CancellationToken ct = default)
    {
        await using var conn = _dbFactory.Create();
        return await conn.QueryAsync<LicitacionParaMonitorDto>(
            LicitacionStoredProcedures.ObtenerParaMonitor,
            new { p_estados = estados },
            commandType: CommandType.Text);
    }

    public async Task<(bool EsNueva, long Id)> AclaracionUpsertAsync(
        string codigoExterno, int codigoAclaracion, string? pregunta, string? respuesta,
        DateTime? fechaPublicacion, DateTime? fechaRespuesta, CancellationToken ct = default)
    {
        await using var conn = _dbFactory.Create();
        var result = await conn.QueryAsync<AclaracionUpsertResult>(
            LicitacionStoredProcedures.AclaracionUpsert,
            new
            {
                p_codigo = codigoExterno,
                p_codigo_aclaracion = codigoAclaracion,
                p_pregunta = pregunta,
                p_respuesta = respuesta,
                p_fecha_publicacion = fechaPublicacion,
                p_fecha_respuesta = fechaRespuesta,
            },
            commandType: CommandType.Text);

        var row = result.FirstOrDefault();
        return (row?.p_es_nueva ?? false, row?.p_id ?? 0);
    }

    public async Task MarcarNotificadaAsync(long id, CancellationToken ct = default)
    {
        await using var conn = _dbFactory.Create();
        await conn.ExecuteAsync(
            LicitacionStoredProcedures.AclaracionMarcarNotificada,
            new { p_id = id },
            commandType: CommandType.Text);
    }

    public async Task<IEnumerable<LicitacionSeguidaDto>> ObtenerSeguidasAsync(
        string usuarioId, CancellationToken ct = default)
    {
        await using var conn = _dbFactory.Create();
        return await conn.QueryAsync<LicitacionSeguidaDto>(
            LicitacionStoredProcedures.ObtenerSeguidas,
            new { p_usuario_id = usuarioId },
            commandType: CommandType.Text);
    }

    private class ToggleResult
    {
        public string? p_accion { get; set; }
        public string? p_error_msg { get; set; }
    }

    private class EsSeguidaResult
    {
        public bool p_es_seguida { get; set; }
    }

    private class AclaracionUpsertResult
    {
        public bool p_es_nueva { get; set; }
        public long p_id { get; set; }
    }
}
