using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace MPM.Modules.Alertas.Services;

/// <summary>
/// Envía notificaciones a Telegram Bot API (User Story 5, pedido interno de Manuel Aliaga
/// 2026-07-06). Es un canal adicional al in-app — nunca debe lanzar una excepción hacia el
/// llamador; un fallo se captura, se loguea, y se reporta como (false, error) para que
/// AlertasMatchingService lo registre sin bloquear el resto del flujo.
/// </summary>
public class TelegramNotificationService(HttpClient httpClient, IConfiguration config, ILogger<TelegramNotificationService> logger)
{
    // Corregido 2026-07-10: verificado en vivo (boton "Me interesa" real en Telegram) que
    // Telegram rechaza "reply_markup": null con "Bad Request: object expected as reply markup" --
    // System.Text.Json por defecto SI escribe la propiedad como null literal en vez de omitirla.
    // Con WhenWritingNull, cuando licitacionId es null (el caso del resumen de "Me interesa",
    // que no reenvia boton), reply_markup se omite del JSON en vez de viajar como null.
    private static readonly JsonSerializerOptions _jsonOptions = new()
    {
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    };

    /// <param name="licitacionId">
    /// 024-inteligencia-competencia-alertas / US2: si se provee, el mensaje incluye un botón
    /// inline "Me interesa" (callback_data = "interesa:{licitacionId}") que TelegramWebhookController
    /// procesa para responder con un resumen rápido sin IA. Null para alertas operativas del
    /// scraper que no están atadas a una licitación específica del usuario.
    /// </param>
    public async Task<(bool Enviada, string? Error)> EnviarAsync(string chatId, string mensaje, long? licitacionId = null, CancellationToken ct = default)
    {
        var botToken = config["Telegram:BotToken"];
        if (string.IsNullOrWhiteSpace(botToken))
            return (false, "Telegram:BotToken no configurado");

        try
        {
            object? replyMarkup = licitacionId.HasValue
                ? new { inline_keyboard = new[] { new[] { new { text = "Me interesa", callback_data = $"interesa:{licitacionId.Value}" } } } }
                : null;
            var payload = new { chat_id = chatId, text = mensaje, parse_mode = "MarkdownV2", reply_markup = replyMarkup };
            var content = new StringContent(JsonSerializer.Serialize(payload, _jsonOptions), System.Text.Encoding.UTF8, "application/json");

            var response = await httpClient.PostAsync(
                $"https://api.telegram.org/bot{botToken}/sendMessage", content, ct);

            if (response.IsSuccessStatusCode)
                return (true, null);

            var body = await response.Content.ReadAsStringAsync(ct);
            logger.LogWarning("Telegram respondió {Status}: {Body}", response.StatusCode, body);
            return (false, $"Telegram respondió {(int)response.StatusCode}");
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Fallo enviando notificación a Telegram (chat {ChatId})", chatId);
            return (false, ex.Message);
        }
    }

    public static string FormatearMensaje(string keyword, string nombreLicitacion, string codigoExterno, MPM.Modules.Alertas.Models.ResumenEnriquecido? resumen)
    {
        // El nombre/código de la licitación es texto libre proveniente de Mercado Público —
        // puede traer '_', '*', '.', etc., que MarkdownV2 interpreta como sintaxis de formato.
        // Sin escapar, Telegram respondía 400 y el mensaje nunca se entregaba (QA BUG-013).
        var lineas = new List<string>
        {
            $"🔔 *Nueva alerta: {EscaparMarkdownV2(keyword)}*",
            $"{EscaparMarkdownV2(nombreLicitacion)} \\({EscaparMarkdownV2(codigoExterno)}\\)",
        };

        if (resumen?.Presupuesto != null) lineas.Add($"Presupuesto: {EscaparMarkdownV2(resumen.Presupuesto)}");
        if (resumen?.EsRenovacion == true) lineas.Add("⚠️ Posible renovación de contrato existente");

        return string.Join("\n", lineas);
    }

    /// <summary>
    /// Escapa los caracteres reservados de MarkdownV2 (spec de Telegram Bot API) en texto libre
    /// que se interpola dentro de un mensaje — nunca aplicar sobre la sintaxis de formato que
    /// se agrega a propósito (los `*` de negrita, por ejemplo).
    /// </summary>
    public static string EscaparMarkdownV2(string texto)
    {
        Span<char> reservados = ['_', '*', '[', ']', '(', ')', '~', '`', '>', '#', '+', '-', '=', '|', '{', '}', '.', '!'];
        var sb = new System.Text.StringBuilder(texto.Length);
        foreach (var c in texto)
        {
            if (reservados.Contains(c)) sb.Append('\\');
            sb.Append(c);
        }
        return sb.ToString();
    }
}
