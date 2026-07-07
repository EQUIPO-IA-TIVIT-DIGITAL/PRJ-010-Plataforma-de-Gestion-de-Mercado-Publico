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
public class TelegramWebhookController(AlertasService service, IConfiguration config, ILogger<TelegramWebhookController> logger) : ControllerBase
{
    [HttpPost("webhook")]
    public async Task<IActionResult> Webhook([FromBody] JsonElement update)
    {
        var secretEsperado = config["Telegram:WebhookSecret"];
        if (!string.IsNullOrEmpty(secretEsperado))
        {
            var secretRecibido = Request.Headers["X-Telegram-Bot-Api-Secret-Token"].ToString();
            if (secretRecibido != secretEsperado)
                return Unauthorized();
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
}
