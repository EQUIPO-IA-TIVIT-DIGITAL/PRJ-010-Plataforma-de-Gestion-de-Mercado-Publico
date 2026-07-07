using System.Security.Cryptography;
using System.Text.Json;
using Microsoft.Extensions.Configuration;
using MPM.Modules.Alertas.Data;
using MPM.Modules.Alertas.Models;

namespace MPM.Modules.Alertas.Services;

public class AlertasService(AlertasHandler handler, SinonimosIaService sinonimosService, IConfiguration config)
{
    private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNameCaseInsensitive = true };

    public async Task<ReglaAlertaDto> CrearAsync(string usuarioId, CrearReglaRequest request, CancellationToken ct = default)
    {
        var id = await handler.CrearAsync(usuarioId, request, ct);

        var sinonimos = await sinonimosService.ExpandirAsync(request.Keyword, ct);
        if (sinonimos != null)
            await handler.GuardarSinonimosAsync(id, sinonimos, ct);

        return new ReglaAlertaDto
        {
            Id = id,
            Keyword = request.Keyword,
            SinonimosIa = sinonimos,
            MontoMinimo = request.MontoMinimo,
            MontoMaximo = request.MontoMaximo,
            TiposLicitacion = request.TiposLicitacion,
            Organismos = request.Organismos,
            Activa = true,
            NotificarTelegram = request.NotificarTelegram,
        };
    }

    public async Task<string?> EditarAsync(long id, string usuarioId, CrearReglaRequest request, CancellationToken ct = default)
    {
        var error = await handler.EditarAsync(id, usuarioId, request, ct);
        if (error != null) return error;

        var sinonimos = await sinonimosService.ExpandirAsync(request.Keyword, ct);
        if (sinonimos != null)
            await handler.GuardarSinonimosAsync(id, sinonimos, ct);

        return null;
    }

    public async Task<List<ReglaAlertaDto>> ListarAsync(string usuarioId, CancellationToken ct = default)
    {
        var rows = await handler.ListarAsync(usuarioId, ct);
        return rows.Select(MapRow).ToList();
    }

    public async Task<HistorialAlertasDto> HistorialAsync(long id, string usuarioId, int page, int pageSize, CancellationToken ct = default)
    {
        var rows = await handler.HistorialAsync(id, usuarioId, page, pageSize, ct);

        return new HistorialAlertasDto
        {
            TotalCount = rows.FirstOrDefault()?.p_total_count ?? 0,
            Items = rows.Select(r => new AlertaDisparadaDto
            {
                Id = r.p_id,
                LicitacionId = r.p_licitacion_id,
                TerminoMatch = r.p_termino_match,
                ResumenEnriquecido = string.IsNullOrEmpty(r.p_resumen_enriquecido)
                    ? null
                    : JsonSerializer.Deserialize<ResumenEnriquecido>(r.p_resumen_enriquecido, JsonOptions),
                EsPrueba = r.p_es_prueba,
                DisparadaEn = r.p_disparada_en,
            }).ToList(),
        };
    }

    public Task GuardarMiTelegramAsync(string usuarioId, string telegramChatId, CancellationToken ct = default) =>
        handler.GuardarChatIdAsync(usuarioId, telegramChatId, ct);

    /// <summary>
    /// Genera un deep link de un solo uso (https://t.me/{bot}?start={token}) para que el
    /// usuario conecte su Telegram con un solo clic, sin copiar/pegar el chat_id a mano.
    /// El bot username se resuelve de config (Telegram:BotUsername) porque la API de
    /// Telegram no lo expone en el token; se configura una vez al crear el bot con BotFather.
    /// </summary>
    public async Task<(string Token, string Url)> GenerarLinkTelegramAsync(string usuarioId, CancellationToken ct = default)
    {
        var token = Convert.ToHexString(RandomNumberGenerator.GetBytes(16));
        await handler.CrearLinkTokenAsync(usuarioId, token, ttlMinutos: 10, ct);

        var botUsername = config["Telegram:BotUsername"] ?? "CU010_bot";
        return (token, $"https://t.me/{botUsername}?start={token}");
    }

    /// <summary>Llamado desde el webhook de Telegram al recibir "/start &lt;token&gt;".</summary>
    public async Task<bool> VincularTelegramPorTokenAsync(string token, string chatId, CancellationToken ct = default)
    {
        var usuarioId = await handler.ConsumirLinkTokenAsync(token, ct);
        if (usuarioId == null) return false;

        await handler.GuardarChatIdAsync(usuarioId, chatId, ct);
        return true;
    }

    public Task<(bool? Activa, string? Error)> ToggleAsync(long id, string usuarioId, CancellationToken ct = default) =>
        handler.ToggleAsync(id, usuarioId, ct);

    public Task<string?> EliminarAsync(long id, string usuarioId, CancellationToken ct = default) =>
        handler.EliminarAsync(id, usuarioId, ct);

    private static ReglaAlertaDto MapRow(ReglaAlertaRow row) => new()
    {
        Id = row.p_id,
        Keyword = row.p_keyword,
        SinonimosIa = string.IsNullOrEmpty(row.p_sinonimos_ia)
            ? null
            : JsonSerializer.Deserialize<List<string>>(row.p_sinonimos_ia, JsonOptions),
        MontoMinimo = row.p_monto_minimo,
        MontoMaximo = row.p_monto_maximo,
        TiposLicitacion = row.p_tipos_licitacion,
        Organismos = row.p_organismos,
        Activa = row.p_activa,
        NotificarTelegram = row.p_notificar_telegram,
    };
}
