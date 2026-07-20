using System.Data;
using System.Text.Json;
using Dapper;
using MPM.Core.Data;
using MPM.Modules.Alertas.Models;

namespace MPM.Modules.Alertas.Data;

public class AlertasHandler(DbConnectionFactory dbFactory)
{
    private readonly DbConnectionFactory _dbFactory = dbFactory;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true,
    };

    public async Task<long> CrearAsync(string usuarioId, CrearReglaRequest request, CancellationToken ct = default)
    {
        await using var conn = _dbFactory.Create();
        var result = await conn.QueryAsync<CrearResult>(
            AlertasStoredProcedures.Crear,
            new
            {
                p_usuario_id = usuarioId,
                p_keyword = request.Keyword,
                p_monto_minimo = request.MontoMinimo,
                p_monto_maximo = request.MontoMaximo,
                p_tipos_licitacion = request.TiposLicitacion,
                p_organismos = request.Organismos,
                p_notificar_telegram = request.NotificarTelegram,
            },
            commandType: CommandType.Text);

        return result.First().p_id;
    }

    public async Task GuardarSinonimosAsync(long id, List<string> sinonimos, CancellationToken ct = default)
    {
        await using var conn = _dbFactory.Create();
        var json = JsonSerializer.Serialize(sinonimos, JsonOptions);
        await conn.ExecuteAsync(
            AlertasStoredProcedures.GuardarSinonimos,
            new { p_id = id, p_sinonimos_ia = json },
            commandType: CommandType.Text);
    }

    public async Task<string?> EditarAsync(long id, string usuarioId, CrearReglaRequest request, CancellationToken ct = default)
    {
        await using var conn = _dbFactory.Create();
        var result = await conn.QueryAsync<ErrorResult>(
            AlertasStoredProcedures.Editar,
            new
            {
                p_id = id,
                p_usuario_id = usuarioId,
                p_keyword = request.Keyword,
                p_monto_minimo = request.MontoMinimo,
                p_monto_maximo = request.MontoMaximo,
                p_tipos_licitacion = request.TiposLicitacion,
                p_organismos = request.Organismos,
                p_notificar_telegram = request.NotificarTelegram,
            },
            commandType: CommandType.Text);

        var error = result.FirstOrDefault()?.p_error_msg;
        return string.IsNullOrEmpty(error) ? null : error;
    }

    public async Task<IEnumerable<ReglaAlertaRow>> ListarAsync(string usuarioId, CancellationToken ct = default)
    {
        await using var conn = _dbFactory.Create();
        return await conn.QueryAsync<ReglaAlertaRow>(
            AlertasStoredProcedures.Listar,
            new { p_usuario_id = usuarioId },
            commandType: CommandType.Text);
    }

    public async Task<IEnumerable<ReglaActivaRow>> ListarActivasAsync(CancellationToken ct = default)
    {
        await using var conn = _dbFactory.Create();
        return await conn.QueryAsync<ReglaActivaRow>(
            AlertasStoredProcedures.ListarActivas,
            commandType: CommandType.Text);
    }

    public async Task<(bool? Activa, string? Error)> ToggleAsync(long id, string usuarioId, CancellationToken ct = default)
    {
        await using var conn = _dbFactory.Create();
        var result = await conn.QueryAsync<ToggleResult>(
            AlertasStoredProcedures.Toggle,
            new { p_id = id, p_usuario_id = usuarioId },
            commandType: CommandType.Text);

        var row = result.FirstOrDefault();
        return (row?.p_activa, row?.p_error_msg);
    }

    public async Task<string?> EliminarAsync(long id, string usuarioId, CancellationToken ct = default)
    {
        await using var conn = _dbFactory.Create();
        var result = await conn.QueryAsync<ErrorResult>(
            AlertasStoredProcedures.Eliminar,
            new { p_id = id, p_usuario_id = usuarioId },
            commandType: CommandType.Text);

        var error = result.FirstOrDefault()?.p_error_msg;
        return string.IsNullOrEmpty(error) ? null : error;
    }

    public async Task<List<HistorialDisparoRow>> HistorialAsync(long reglaId, string usuarioId, int page, int pageSize, CancellationToken ct = default)
    {
        await using var conn = _dbFactory.Create();
        var result = await conn.QueryAsync<HistorialDisparoRow>(
            AlertasStoredProcedures.Historial,
            new { p_regla_id = reglaId, p_usuario_id = usuarioId, p_page = page, p_page_size = pageSize },
            commandType: CommandType.Text);

        return result.ToList();
    }

    public async Task<bool> ExisteDisparoAsync(long reglaId, long licitacionId, CancellationToken ct = default)
    {
        await using var conn = _dbFactory.Create();
        var result = await conn.QueryAsync<ExisteResult>(
            AlertasStoredProcedures.ExisteParaLicitacion,
            new { p_regla_id = reglaId, p_licitacion_id = licitacionId },
            commandType: CommandType.Text);

        return result.FirstOrDefault()?.p_existe ?? false;
    }

    public async Task<long?> RegistrarDisparoAsync(
        long reglaId, long licitacionId, string? terminoMatch, ResumenEnriquecido? resumen,
        long? notificacionInAppId, bool esPrueba, CancellationToken ct = default)
    {
        await using var conn = _dbFactory.Create();
        var resumenJson = resumen != null ? JsonSerializer.Serialize(resumen, JsonOptions) : null;

        var result = await conn.QueryAsync<CrearResult>(
            AlertasStoredProcedures.RegistrarDisparo,
            new
            {
                p_regla_id = reglaId,
                p_licitacion_id = licitacionId,
                p_termino_match = terminoMatch,
                p_resumen_enriquecido = resumenJson,
                p_notificacion_inapp_id = notificacionInAppId,
                p_es_prueba = esPrueba,
            },
            commandType: CommandType.Text);

        return result.FirstOrDefault()?.p_id;
    }

    public async Task MarcarTelegramAsync(long alertaDisparadaId, bool enviada, string? error, CancellationToken ct = default)
    {
        await using var conn = _dbFactory.Create();
        await conn.ExecuteAsync(
            AlertasStoredProcedures.MarcarTelegram,
            new { p_id = alertaDisparadaId, p_enviada = enviada, p_error = error },
            commandType: CommandType.Text);
    }

    public async Task GuardarChatIdAsync(string usuarioId, string telegramChatId, CancellationToken ct = default)
    {
        await using var conn = _dbFactory.Create();
        await conn.ExecuteAsync(
            AlertasStoredProcedures.GuardarChatId,
            new { p_usuario_id = usuarioId, p_telegram_chat_id = telegramChatId },
            commandType: CommandType.Text);
    }

    public async Task<IEnumerable<(string UsuarioId, string? TelegramChatId, string? EmailAlertas)>> ListarAccountManagersAsync(CancellationToken ct = default)
    {
        await using var conn = _dbFactory.Create();
        var result = await conn.QueryAsync<DestinatarioRow>(
            AlertasStoredProcedures.ListarAccountManagers,
            commandType: CommandType.Text);

        return result.Select(r => (r.p_usuario_id, r.p_telegram_chat_id, r.p_email_alertas));
    }

    public async Task GuardarEmailAsync(string usuarioId, string emailAlertas, CancellationToken ct = default)
    {
        await using var conn = _dbFactory.Create();
        await conn.ExecuteAsync(
            AlertasStoredProcedures.GuardarEmail,
            new { p_usuario_id = usuarioId, p_email_alertas = emailAlertas },
            commandType: CommandType.Text);
    }

    public async Task CrearLinkTokenAsync(string usuarioId, string token, int ttlMinutos, CancellationToken ct = default)
    {
        await using var conn = _dbFactory.Create();
        await conn.ExecuteAsync(
            AlertasStoredProcedures.CrearLinkToken,
            new { p_usuario_id = usuarioId, p_token = token, p_ttl_minutos = ttlMinutos },
            commandType: CommandType.Text);
    }

    public async Task<string?> ConsumirLinkTokenAsync(string token, CancellationToken ct = default)
    {
        await using var conn = _dbFactory.Create();
        var result = await conn.QueryAsync<LinkTokenResult>(
            AlertasStoredProcedures.ConsumirLinkToken,
            new { p_token = token },
            commandType: CommandType.Text);

        return result.FirstOrDefault()?.p_usuario_id;
    }

    private class LinkTokenResult
    {
        public string p_usuario_id { get; set; } = "";
    }

    private class CrearResult
    {
        public long p_id { get; set; }
    }

    private class ErrorResult
    {
        public string? p_error_msg { get; set; }
    }

    private class ToggleResult
    {
        public bool? p_activa { get; set; }
        public string? p_error_msg { get; set; }
    }

    private class ExisteResult
    {
        public bool p_existe { get; set; }
    }

    private class DestinatarioRow
    {
        public string p_usuario_id { get; set; } = "";
        public string? p_telegram_chat_id { get; set; }
        public string? p_email_alertas { get; set; }
    }

    // 029-fix-hallazgos-code-review-competidores-alertas (FR-004/US4): antes le faltaba el guard
    // deleted_at IS NULL que sí tiene la query equivalente en LicitacionHandler.cs -- un match de
    // Alertas en curso podía "resucitar" organismo/monto/raw_data en una licitación ya eliminada
    // (soft-delete) por deduplicación o reconciliación de catálogo.
    public async Task ActualizarLicitacionEnCalienteAsync(
        string codigoExterno, string? organismo, string? unidadTecnica, decimal? montoEstimado, string? descripcion, string rawData)
    {
        await using var conn = _dbFactory.Create();
        await conn.ExecuteAsync(
            @"UPDATE licitaciones 
              SET organismo = COALESCE(organismo, @organismo), 
                  unidad_tecnica = COALESCE(unidad_tecnica, @unidadTecnica), 
                  monto_estimado = COALESCE(monto_estimado, @montoEstimado), 
                  descripcion = COALESCE(descripcion, @descripcion), 
                  raw_data = CASE 
                      WHEN raw_data IS NOT NULL AND (raw_data->'Comprador') IS NOT NULL THEN raw_data 
                      ELSE @rawData::JSONB 
                  END,
                  updated_at = CURRENT_TIMESTAMP
              WHERE codigo_externo = @codigoExterno AND deleted_at IS NULL;",
            new
            {
                codigoExterno,
                organismo,
                unidadTecnica,
                montoEstimado,
                descripcion,
                rawData
            },
            commandType: CommandType.Text);
    }
}

public class ReglaAlertaRow
{
    public long p_id { get; set; }
    public string p_keyword { get; set; } = "";
    public string? p_sinonimos_ia { get; set; }
    public decimal? p_monto_minimo { get; set; }
    public decimal? p_monto_maximo { get; set; }
    public string[]? p_tipos_licitacion { get; set; }
    public string[]? p_organismos { get; set; }
    public bool p_activa { get; set; }
    public bool p_notificar_telegram { get; set; }
}

public class ReglaActivaRow
{
    public long p_id { get; set; }
    public string p_usuario_id { get; set; } = "";
    public string p_keyword { get; set; } = "";
    public string? p_sinonimos_ia { get; set; }
    public decimal? p_monto_minimo { get; set; }
    public decimal? p_monto_maximo { get; set; }
    public string[]? p_tipos_licitacion { get; set; }
    public string[]? p_organismos { get; set; }
    public bool p_notificar_telegram { get; set; }
}

public class HistorialDisparoRow
{
    public long p_id { get; set; }
    public long p_licitacion_id { get; set; }
    public string? p_termino_match { get; set; }
    public string? p_resumen_enriquecido { get; set; }
    public bool p_es_prueba { get; set; }
    public DateTime p_disparada_en { get; set; }
    public long p_total_count { get; set; }
}
