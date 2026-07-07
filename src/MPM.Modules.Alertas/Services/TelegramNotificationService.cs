using System.Text.Json;
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
    public async Task<(bool Enviada, string? Error)> EnviarAsync(string chatId, string mensaje, CancellationToken ct = default)
    {
        var botToken = config["Telegram:BotToken"];
        if (string.IsNullOrWhiteSpace(botToken))
            return (false, "Telegram:BotToken no configurado");

        try
        {
            var payload = new { chat_id = chatId, text = mensaje, parse_mode = "Markdown" };
            var content = new StringContent(JsonSerializer.Serialize(payload), System.Text.Encoding.UTF8, "application/json");

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
        var lineas = new List<string>
        {
            $"🔔 *Nueva alerta: {keyword}*",
            $"{nombreLicitacion} ({codigoExterno})",
        };

        if (resumen?.Presupuesto != null) lineas.Add($"Presupuesto: {resumen.Presupuesto}");
        if (resumen?.EsRenovacion == true) lineas.Add("⚠️ Posible renovación de contrato existente");

        return string.Join("\n", lineas);
    }
}
