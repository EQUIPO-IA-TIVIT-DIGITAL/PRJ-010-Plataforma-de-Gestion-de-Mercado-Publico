using System.Text.Json;
using System.Net.Http;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Configuration;
using MPM.Modules.Alertas.Data;
using MPM.Modules.Alertas.Models;
using MPM.Modules.Notificaciones.Services;

namespace MPM.Modules.Alertas.Services;

/// <summary>
/// Motor de matching de Alertas (User Stories 1, 3, 4, 5). Se invoca desde
/// <c>SyncEngineService</c> (módulo Licitaciones) al final de cada ciclo de sync, sobre las
/// licitaciones nuevas/actualizadas de ese ciclo — no tiene su propio Timer/IHostedService
/// (research.md/plan.md de 003: evita un segundo proceso de fondo compitiendo por recursos).
/// </summary>
public class AlertasMatchingService(
    AlertasHandler handler,
    AlertaEnriquecimientoService enriquecimiento,
    TelegramNotificationService telegram,
    EmailNotificationService email,
    NotificacionesService notificaciones,
    ILogger<AlertasMatchingService> logger,
    IServiceProvider serviceProvider,
    IHttpClientFactory httpClientFactory)
{
    private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNameCaseInsensitive = true };

    public async Task EvaluarLicitacionesAsync(IEnumerable<LicitacionParaMatching> licitaciones, CancellationToken ct = default)
    {
        var reglas = (await handler.ListarActivasAsync(ct)).ToList();
        if (reglas.Count == 0) return;

        // BUG-012 (QA): antes se consultaba una vez POR LICITACIÓN CON MATCH dentro de
        // ProcesarGrupoAsync; se sube al nivel de ciclo y se pasa como parámetro, sin cambiar
        // el resultado — solo el número de consultas a la BD.
        var destinatarios = (await handler.ListarAccountManagersAsync(ct)).ToList();

        foreach (var licitacion in licitaciones)
        {
            try
            {
                await EvaluarUnaLicitacionAsync(licitacion, reglas, destinatarios, esPrueba: false, ct);
            }
            catch (Exception ex)
            {
                // Una licitación que falla no debe tumbar el resto del ciclo de sync.
                logger.LogError(ex, "Fallo evaluando alertas para licitación {Codigo}", licitacion.CodigoExterno);
            }
        }
    }

    public async Task<ProbarAlertaResponse> ProbarAsync(long reglaId, string usuarioId, LicitacionParaMatching licitacion, CancellationToken ct = default)
    {
        var reglas = (await handler.ListarActivasAsync(ct))
            .Where(r => r.p_id == reglaId && r.p_usuario_id == usuarioId)
            .ToList();

        if (reglas.Count == 0)
            return new ProbarAlertaResponse(0, true, false, false, "Regla no encontrada, no pertenece al usuario, o está pausada");

        var destinatarios = (await handler.ListarAccountManagersAsync(ct)).ToList();
        var resultado = await EvaluarUnaLicitacionAsync(licitacion, reglas, destinatarios, esPrueba: true, ct, forzarMatch: true);
        return resultado ?? new ProbarAlertaResponse(0, true, false, false, "No se pudo generar la alerta de prueba");
    }

    private async Task<ProbarAlertaResponse?> EvaluarUnaLicitacionAsync(
        LicitacionParaMatching licitacion, List<ReglaActivaRow> reglas, List<(string UsuarioId, string? TelegramChatId, string? EmailAlertas)> destinatarios, bool esPrueba, CancellationToken ct, bool forzarMatch = false)
    {
        var matches = new List<(ReglaActivaRow Regla, string Termino)>();

        foreach (var regla in reglas)
        {
            var termino = forzarMatch ? (regla.p_keyword) : EvaluarMatch(regla, licitacion);
            if (termino == null) continue;

            // Deduplicación: no disparar la misma regla dos veces para la misma licitación
            // (spec US1) — salvo que sea una prueba explícita para demo (T028: no contamina
            // el historial real, pero sí puede repetirse).
            if (!esPrueba && await handler.ExisteDisparoAsync(regla.p_id, licitacion.LicitacionId, ct))
                continue;

            matches.Add((regla, termino));
        }

        if (matches.Count == 0) return esPrueba ? null : null;

        // --- ENRIQUECIMIENTO EN CALIENTE ---
        // Si hay un match de alertas y el organismo está vacío (lo que indica que proviene de la API masiva resumida)
        if (string.IsNullOrWhiteSpace(licitacion.Organismo))
        {
            try
            {
                var config = serviceProvider.GetRequiredService<IConfiguration>();
                var ticket = config["MP_TICKET"];

                if (!string.IsNullOrEmpty(ticket))
                {
                    logger.LogInformation("Enriqueciendo licitación disparada {Codigo} en caliente...", licitacion.CodigoExterno);
                    var url = $"https://api.mercadopublico.cl/servicios/v1/publico/licitaciones.json?ticket={ticket}&codigo={licitacion.CodigoExterno}";
                    
                    using var httpClient = httpClientFactory.CreateClient();
                    var response = await httpClient.GetAsync(url, ct);
                    
                    if (response.IsSuccessStatusCode)
                    {
                        var json = await response.Content.ReadAsStringAsync(ct);
                        using var doc = JsonDocument.Parse(json);
                        var root = doc.RootElement;
                        
                        if (root.TryGetProperty("Listado", out var listadoProp) && listadoProp.ValueKind == JsonValueKind.Array && listadoProp.GetArrayLength() > 0)
                        {
                            var item = listadoProp[0];
                            string? organismo = null;
                            string? unidadTecnica = null;
                            if (item.TryGetProperty("Comprador", out var compradorProp) && compradorProp.ValueKind == JsonValueKind.Object)
                            {
                                if (compradorProp.TryGetProperty("NombreOrganismo", out var orgProp)) organismo = orgProp.GetString();
                                if (compradorProp.TryGetProperty("NombreUnidad", out var uniProp)) unidadTecnica = uniProp.GetString();
                            }

                            string? descripcion = null;
                            if (item.TryGetProperty("Descripcion", out var descProp)) descripcion = descProp.GetString();

                            decimal? montoEstimado = null;
                            if (item.TryGetProperty("MontoEstimado", out var montoProp) && (montoProp.ValueKind == JsonValueKind.Number || (montoProp.ValueKind == JsonValueKind.String && decimal.TryParse(montoProp.GetString(), out _))))
                            {
                                montoEstimado = montoProp.ValueKind == JsonValueKind.Number 
                                    ? montoProp.GetDecimal() 
                                    : decimal.Parse(montoProp.GetString()!);
                            }

                            // 1. Guardamos en la base de datos de inmediato usando el handler
                            await handler.ActualizarLicitacionEnCalienteAsync(
                                licitacion.CodigoExterno,
                                organismo,
                                unidadTecnica,
                                montoEstimado,
                                descripcion,
                                item.GetRawText()
                            );

                            // 2. Actualizamos el objeto en memoria para el envío de notificaciones
                            licitacion = licitacion with
                            {
                                Descripcion = descripcion ?? licitacion.Descripcion,
                                Monto = montoEstimado ?? licitacion.Monto,
                                Organismo = organismo ?? licitacion.Organismo
                            };
                            logger.LogInformation("Licitación {Codigo} enriquecida exitosamente en caliente.", licitacion.CodigoExterno);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Error enriqueciendo licitación {Codigo} en caliente", licitacion.CodigoExterno);
            }
        }
        // ------------------------------------

        ProbarAlertaResponse? respuestaPrueba = null;

        foreach (var grupoUsuario in matches.GroupBy(m => m.Regla.p_usuario_id))
        {
            var resultado = await ProcesarGrupoAsync(grupoUsuario.Key, grupoUsuario.ToList(), licitacion, destinatarios, esPrueba, ct);
            if (esPrueba) respuestaPrueba = resultado;
        }

        return respuestaPrueba;
    }

    private async Task<ProbarAlertaResponse> ProcesarGrupoAsync(
        string usuarioId, List<(ReglaActivaRow Regla, string Termino)> grupo, LicitacionParaMatching licitacion,
        List<(string UsuarioId, string? TelegramChatId, string? EmailAlertas)> destinatarios, bool esPrueba, CancellationToken ct)
    {
        var resumen = await enriquecimiento.GenerarAsync(licitacion, ct);

        var terminos = string.Join(", ", grupo.Select(g => g.Termino).Distinct());
        var titulo = grupo.Count == 1
            ? $"Nueva licitación: {grupo[0].Regla.p_keyword}"
            : $"Nueva licitación coincide con {grupo.Count} alertas";
        var mensaje = $"{licitacion.Nombre} ({licitacion.CodigoExterno}) — coincide por: {terminos}";

        var (notifId, notifError) = await notificaciones.CrearAsync(
            usuarioId, "alerta_licitacion", titulo, mensaje,
            new { licitacionId = licitacion.LicitacionId, codigoExterno = licitacion.CodigoExterno, terminos, esPrueba });

        var notificacionCreada = notifError == null;

        long? primerDisparoId = null;
        foreach (var (regla, termino) in grupo)
        {
            var disparoId = await handler.RegistrarDisparoAsync(
                regla.p_id, licitacion.LicitacionId, termino, resumen,
                notificacionCreada ? notifId : null, esPrueba, ct);
            primerDisparoId ??= disparoId;
        }

        // T032: ademas del dueno de la regla, notificar a TODOS los account managers de
        // gobierno configurados en alertas_destinatarios (pedido explicito de Francisco,
        // 2026-07-01) -- antes solo llegaba al creador de la regla. `destinatarios` ahora se
        // recibe como parámetro (una sola consulta por ciclo, no una por licitación — BUG-012).
        foreach (var destinatario in destinatarios.Where(d => d.UsuarioId != usuarioId))
        {
            await notificaciones.CrearAsync(
                destinatario.UsuarioId, "alerta_licitacion", titulo, mensaje,
                new { licitacionId = licitacion.LicitacionId, codigoExterno = licitacion.CodigoExterno, terminos, esPrueba });
        }

        var telegramEnviada = false;
        string? telegramError = null;

        if (grupo.Any(g => g.Regla.p_notificar_telegram))
        {
            var mensajeTelegram = TelegramNotificationService.FormatearMensaje(terminos, licitacion.Nombre, licitacion.CodigoExterno, resumen);

            foreach (var destinatario in destinatarios)
            {
                if (string.IsNullOrEmpty(destinatario.TelegramChatId)) continue;

                var (enviada, error) = await telegram.EnviarAsync(destinatario.TelegramChatId, mensajeTelegram, licitacion.LicitacionId, ct);
                telegramEnviada = telegramEnviada || enviada;
                telegramError ??= error;
            }

            if (primerDisparoId.HasValue)
                await handler.MarcarTelegramAsync(primerDisparoId.Value, telegramEnviada, telegramError, ct);
        }

        // US3 (024-inteligencia-competencia-alertas): canal de correo, independiente del de
        // Telegram — FR-011: el fallo o ausencia de un canal no debe impedir el intento en el
        // otro. No se gatea por p_notificar_telegram (ese flag es específico de Telegram).
        foreach (var destinatario in destinatarios)
        {
            if (string.IsNullOrEmpty(destinatario.EmailAlertas)) continue;

            await email.EnviarAsync(
                destinatario.EmailAlertas, terminos, licitacion.Nombre, licitacion.CodigoExterno,
                licitacion.Monto?.ToString("N0"), ct);
        }

        return new ProbarAlertaResponse(primerDisparoId ?? 0, esPrueba, notificacionCreada, telegramEnviada, telegramError);
    }

    /// <summary>
    /// Evalúa si una licitación coincide con una regla: filtros de monto/tipo/organismo (todos
    /// los configurados deben cumplirse) MÁS coincidencia de texto por keyword literal o algún
    /// sinónimo IA (research.md §1). Retorna el término que disparó el match, o null.
    /// </summary>
    internal static string? EvaluarMatch(ReglaActivaRow regla, LicitacionParaMatching licitacion)
    {
        if (regla.p_monto_minimo.HasValue && (licitacion.Monto ?? 0) < regla.p_monto_minimo.Value) return null;
        if (regla.p_monto_maximo.HasValue && (licitacion.Monto ?? decimal.MaxValue) > regla.p_monto_maximo.Value) return null;
        if (regla.p_tipos_licitacion is { Length: > 0 } tipos && (licitacion.TipoLicitacion == null || !tipos.Contains(licitacion.TipoLicitacion, StringComparer.OrdinalIgnoreCase))) return null;
        if (regla.p_organismos is { Length: > 0 } organismos && (licitacion.Organismo == null || !organismos.Contains(licitacion.Organismo, StringComparer.OrdinalIgnoreCase))) return null;

        var texto = $"{licitacion.Nombre} {licitacion.Descripcion}".ToLowerInvariant();

        if (texto.Contains(regla.p_keyword.ToLowerInvariant()))
            return regla.p_keyword;

        if (!string.IsNullOrEmpty(regla.p_sinonimos_ia))
        {
            try
            {
                var sinonimos = JsonSerializer.Deserialize<List<string>>(regla.p_sinonimos_ia, JsonOptions) ?? [];
                var match = sinonimos.FirstOrDefault(s => texto.Contains(s.ToLowerInvariant()));
                if (match != null) return match;
            }
            catch (JsonException)
            {
                // sinonimos_ia corrupto/mal formado: se ignora la expansión para esta regla
                // en vez de tumbar el matching de toda la licitación.
            }
        }

        return null;
    }
}
