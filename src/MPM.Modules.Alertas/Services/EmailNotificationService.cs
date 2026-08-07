using System.Net;
using Microsoft.Extensions.Logging;
using MPM.Shared.Services;

namespace MPM.Modules.Alertas.Services;

/// <summary>
/// 024-inteligencia-competencia-alertas / US3: canal de alertas por correo, adicional a
/// Telegram (no lo reemplaza). Reusa IEmailService/SmtpEmailService ya existente en
/// MPM.Shared (usado hoy por Auth para reset de contraseña, research.md R7) -- cero
/// infraestructura nueva. Nunca lanza hacia el llamador, mismo contrato que
/// TelegramNotificationService.EnviarAsync (FR-011: el fallo de un canal no bloquea al otro).
/// </summary>
public class EmailNotificationService(IEmailService emailService, ILogger<EmailNotificationService> logger)
{
    public async Task<(bool Enviada, string? Error)> EnviarAsync(
        string toEmail, string keyword, string nombreLicitacion, string codigoExterno, string? presupuesto,
        string? organismo = null, DateTime? fechaCierre = null, string? link = null, CancellationToken ct = default)
    {
        try
        {
            var subject = $"Nueva alerta: {keyword}";
            // 032-mejora-alertas-correo (US2): organismo/fechaCierre/link ya vienen disponibles
            // desde el sync (contracts/correo-alerta-formato.md) -- cada campo se omite
            // prolijamente cuando no hay dato, sin mostrar texto vacío/roto (FR-006).
            var html = $"""
                <h2>🔔 Nueva alerta: {WebUtility.HtmlEncode(keyword)}</h2>
                <p><strong>{WebUtility.HtmlEncode(nombreLicitacion)}</strong> ({WebUtility.HtmlEncode(codigoExterno)})</p>
                {(!string.IsNullOrWhiteSpace(organismo) ? $"<p>Organismo: {WebUtility.HtmlEncode(organismo)}</p>" : "")}
                {(fechaCierre.HasValue ? $"<p>Cierra: {fechaCierre.Value:dd-MM-yyyy}</p>" : "")}
                {(presupuesto != null ? $"<p>Presupuesto: {WebUtility.HtmlEncode(presupuesto)}</p>" : "")}
                {(!string.IsNullOrWhiteSpace(link) ? $"""<p><a href="{WebUtility.HtmlEncode(link)}">Ver ficha en Mercado Público</a></p>""" : "")}
                """;

            await emailService.SendEmailAsync(toEmail, subject, html);
            return (true, null);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Fallo enviando alerta por correo a {Email}", toEmail);
            return (false, ex.Message);
        }
    }
}
