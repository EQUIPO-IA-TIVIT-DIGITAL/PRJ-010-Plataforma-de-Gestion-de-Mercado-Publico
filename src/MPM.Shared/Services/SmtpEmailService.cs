using MailKit.Net.Smtp;
using MailKit.Security;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using MimeKit;

namespace MPM.Shared.Services;

/// <summary>
/// Implementación de servicio de email usando SMTP
/// </summary>
public class SmtpEmailService : IEmailService
{
    private readonly IConfiguration _config;
    private readonly ILogger<SmtpEmailService> _logger;

    public SmtpEmailService(IConfiguration config, ILogger<SmtpEmailService> logger)
    {
        _config = config;
        _logger = logger;
    }

    public async Task SendPasswordResetEmailAsync(string toEmail, string resetUrl, string userName = "")
    {
        var displayName = string.IsNullOrEmpty(userName) ? toEmail : userName;
        
        var htmlBody = $@"
        <!DOCTYPE html>
        <html>
        <head>
            <style>
                body {{ font-family: Arial, sans-serif; line-height: 1.6; color: #333; }}
                .container {{ max-width: 600px; margin: 0 auto; padding: 20px; }}
                .header {{ background-color: #1a1a2e; color: white; padding: 20px; text-align: center; }}
                .content {{ background-color: #f9f9f9; padding: 30px; }}
                .button {{ 
                    display: inline-block; 
                    background-color: #E30613; 
                    color: white; 
                    padding: 12px 30px; 
                    text-decoration: none; 
                    border-radius: 6px;
                    margin: 20px 0;
                }}
                .footer {{ text-align: center; padding: 20px; color: #666; font-size: 12px; }}
            </style>
        </head>
        <body>
            <div class='container'>
                <div class='header'>
                    <h2>MPM - Mercado Público</h2>
                </div>
                <div class='content'>
                    <h3>Recuperación de contraseña</h3>
                    <p>Hola {displayName},</p>
                    <p>Has solicitado restablecer tu contraseña. Haz clic en el siguiente botón para continuar:</p>
                    <p style='text-align: center;'>
                        <a href='{resetUrl}' class='button'>Restablecer contraseña</a>
                    </p>
                    <p>O copia y pega este enlace en tu navegador:</p>
                    <p style='word-break: break-all; background: #eee; padding: 10px; border-radius: 4px;'>{resetUrl}</p>
                    <p><strong>Este enlace expirará en 1 hora.</strong></p>
                    <p>Si no solicitaste este cambio, puedes ignorar este email de forma segura.</p>
                </div>
                <div class='footer'>
                    <p>Este es un email automático, por favor no respondas a este mensaje.</p>
                    <p>&copy; {DateTime.Now.Year} MPM - Mercado Público</p>
                </div>
            </div>
        </body>
        </html>";

        await SendEmailAsync(toEmail, "Recuperación de contraseña - MPM", htmlBody);
    }

    public async Task SendEmailAsync(string toEmail, string subject, string htmlBody)
    {
        var smtpHost = _config["Smtp:Host"];
        var smtpPort = int.Parse(_config["Smtp:Port"] ?? "587");
        var smtpUser = _config["Smtp:Username"];
        var smtpPass = _config["Smtp:Password"];
        var fromEmail = _config["Smtp:FromEmail"] ?? "noreply@mpm.cl";
        var fromName = _config["Smtp:FromName"] ?? "MPM - Mercado Público";
        var enableSsl = bool.Parse(_config["Smtp:EnableSsl"] ?? "true");

        // Si no hay configuración SMTP, solo loguear (modo desarrollo)
        if (string.IsNullOrEmpty(smtpHost))
        {
            _logger.LogWarning("SMTP no configurado. Email para {To}: Subject='{Subject}'", toEmail, subject);
            _logger.LogInformation("Email body preview: {Body}", htmlBody.Substring(0, Math.Min(500, htmlBody.Length)));
            return;
        }

        var message = new MimeMessage();
        message.From.Add(new MailboxAddress(fromName, fromEmail));
        message.To.Add(MailboxAddress.Parse(toEmail));
        message.Subject = subject;
        message.Body = new TextPart("html") { Text = htmlBody };

        var secureSocketOptions = enableSsl ? SecureSocketOptions.StartTls : SecureSocketOptions.None;

        // Brevo (relay SMTP usado en 024-inteligencia-competencia-alertas / US3) reparte
        // smtp-relay.brevo.com entre varios nodos backend detrás de balanceo -- al menos uno
        // observado en pruebas todavía sirve un certificado viejo de sendinblue.com sin
        // brevo.com en el SAN (RemoteCertificateNameMismatch), de forma intermitente y no
        // reproducible con un solo intento. 3 reintentos con backoff corto absorben ese nodo
        // problemático sin bloquear el flujo de alertas (que ya es best-effort).
        const int maxIntentos = 3;
        for (var intento = 1; intento <= maxIntentos; intento++)
        {
            try
            {
                // System.Net.Mail.SmtpClient (legacy) no manda SNI en el ClientHello de TLS, lo
                // que rompe contra hosts SMTP detrás de un balanceador ruteado por SNI. MailKit
                // sí implementa SNI correctamente -- reemplazo directo, mismo comportamiento
                // hacia afuera (IEmailService no cambia).
                using var client = new SmtpClient();
                await client.ConnectAsync(smtpHost, smtpPort, secureSocketOptions);
                if (!string.IsNullOrEmpty(smtpUser))
                    await client.AuthenticateAsync(smtpUser, smtpPass);

                await client.SendAsync(message);
                await client.DisconnectAsync(true);

                _logger.LogInformation("Email enviado a {To}: {Subject} (intento {Intento})", toEmail, subject, intento);
                return;
            }
            catch (AuthenticationException ex)
            {
                // Credenciales rechazadas: no es transitorio, reintentar no cambia el resultado.
                _logger.LogError(ex, "Error enviando email a {To}: {Subject} (autenticación rechazada, sin reintentos)", toEmail, subject);
                return;
            }
            catch (Exception ex) when (intento < maxIntentos)
            {
                _logger.LogWarning(ex, "Fallo transitorio enviando email a {To} (intento {Intento}/{Max}), reintentando", toEmail, intento, maxIntentos);
                await Task.Delay(TimeSpan.FromSeconds(intento));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error enviando email a {To}: {Subject} (agotados {Max} intentos)", toEmail, subject, maxIntentos);
                // No lanzar excepción para no afectar el flujo principal (FR-011: el fallo del
                // canal de correo no debe bloquear el resto del ciclo de alertas).
            }
        }
    }
}
