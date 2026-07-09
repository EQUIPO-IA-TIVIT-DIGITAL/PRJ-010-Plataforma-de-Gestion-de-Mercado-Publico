using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using MPM.Modules.Alertas.Services;

namespace MPM.Modules.Alertas.Controllers;

/// <summary>
/// Recibe los updates que Telegram empuja al configurar setWebhook (ver runbook de
/// producción). Requiere una URL HTTPS pública -- no funciona contra localhost, por lo
/// que en desarrollo local la vinculación de chat_id sigue siendo manual vía "Mi Telegram".
/// Sin [Authorize]: Telegram no manda JWT, se valida con el secret token que Telegram
/// reenvía en el header (configurado al llamar setWebhook con secret_token).
/// </summary>
[ApiController]
[Route("api/v1/telegram")]
[AllowAnonymous]
public class TelegramWebhookController(
    AlertasService service,
    ResumenLicitacionService resumenService,
    TelegramNotificationService telegram,
    IConfiguration config,
    ILogger<TelegramWebhookController> logger) : ControllerBase
{
    [HttpPost("webhook")]
    public async Task<IActionResult> Webhook([FromBody] JsonElement update)
    {
        // Fail-closed: si el secret no está configurado, la petición se rechaza en vez de
        // procesarse sin validar — antes, un Telegram:WebhookSecret vacío omitía la validación
        // por completo y cualquiera que conociera la URL podía vincular chats arbitrarios
        // (QA BUG-009). Comparación en tiempo constante para evitar timing attacks sobre el
        // secreto. Se aplica por igual a mensajes de texto y a callback_query (botones inline).
        var secretEsperado = config["Telegram:WebhookSecret"];
        if (string.IsNullOrEmpty(secretEsperado) || !SecretCoincide(Request.Headers["X-Telegram-Bot-Api-Secret-Token"].ToString(), secretEsperado))
            return Unauthorized();

        // 024-inteligencia-competencia-alertas / US2: botón inline "Me interesa" -- Telegram
        // manda esto como callback_query, no como message.
        if (update.TryGetProperty("callback_query", out var callbackQuery) && callbackQuery.ValueKind == JsonValueKind.Object)
        {
            await ManejarCallbackQueryAsync(callbackQuery);
            return Ok();
        }

        if (!update.TryGetProperty("message", out var message) || message.ValueKind != JsonValueKind.Object)
            return Ok();

        if (!message.TryGetProperty("text", out var textEl) || textEl.ValueKind != JsonValueKind.String)
            return Ok();

        if (!message.TryGetProperty("chat", out var chat) || chat.ValueKind != JsonValueKind.Object ||
            !chat.TryGetProperty("id", out var chatIdEl))
            return Ok();

        var text = textEl.GetString() ?? "";
        const string prefijo = "/start ";
        if (!text.StartsWith(prefijo, StringComparison.Ordinal))
            return Ok();

        var token = text[prefijo.Length..].Trim();
        var chatId = chatIdEl.GetRawText();

        var vinculado = await service.VincularTelegramPorTokenAsync(token, chatId);
        if (!vinculado)
            logger.LogWarning("Token de vinculación de Telegram inválido o expirado: {Token}", token);

        // Telegram solo requiere 200 OK; el resultado de la vinculación no se le reporta al bot.
        return Ok();
    }

    /// <summary>
    /// FR-007/FR-008: procesa el click en "Me interesa" -- arma el resumen desde datos ya
    /// sincronizados de Mercado Público (ResumenLicitacionService), SIN invocar IA, y lo responde
    /// en el mismo chat. Un doble click sobre el mismo botón simplemente reenvía el mismo
    /// resumen (idempotente, sin costo de IA de por medio) -- satisface el edge case del spec
    /// sin necesitar lógica extra de deduplicación.
    /// </summary>
    private async Task ManejarCallbackQueryAsync(JsonElement callbackQuery)
    {
        if (!callbackQuery.TryGetProperty("data", out var dataEl) || dataEl.ValueKind != JsonValueKind.String)
            return;

        var data = dataEl.GetString() ?? "";
        const string prefijo = "interesa:";
        if (!data.StartsWith(prefijo, StringComparison.Ordinal))
            return;

        if (!long.TryParse(data[prefijo.Length..], out var licitacionId))
        {
            logger.LogWarning("callback_query 'interesa:' con licitacionId no numérico: {Data}", data);
            return;
        }

        if (!callbackQuery.TryGetProperty("message", out var msg) || msg.ValueKind != JsonValueKind.Object ||
            !msg.TryGetProperty("chat", out var chat) || chat.ValueKind != JsonValueKind.Object ||
            !chat.TryGetProperty("id", out var chatIdEl))
            return;

        var chatId = chatIdEl.GetRawText();

        var resumen = await resumenService.ObtenerResumenPorIdAsync(licitacionId);
        var mensaje = resumen ?? "No se pudo obtener el resumen de esta licitación en este momento — puede que ya no esté disponible en Mercado Público.";

        var (enviado, error) = await telegram.EnviarAsync(chatId, mensaje);
        if (!enviado)
            logger.LogWarning("No se pudo enviar el resumen de 'Me interesa' (licitación {Id}, chat {ChatId}): {Error}", licitacionId, chatId, error);
    }

    private static bool SecretCoincide(string recibido, string esperado)
    {
        var recibidoBytes = Encoding.UTF8.GetBytes(recibido);
        var esperadoBytes = Encoding.UTF8.GetBytes(esperado);
        // FixedTimeEquals exige igual longitud; si difiere, ya no coinciden (sin filtrar
        // cuánto tarda la comparación según la longitud, que no es el secreto en sí).
        return recibidoBytes.Length == esperadoBytes.Length && CryptographicOperations.FixedTimeEquals(recibidoBytes, esperadoBytes);
    }
}
