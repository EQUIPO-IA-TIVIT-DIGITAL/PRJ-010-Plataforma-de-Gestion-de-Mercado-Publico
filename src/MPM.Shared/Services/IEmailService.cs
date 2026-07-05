namespace MPM.Shared.Services;

/// <summary>
/// Servicio para envío de emails
/// </summary>
public interface IEmailService
{
    /// <summary>
    /// Envía un email de recuperación de contraseña
    /// </summary>
    Task SendPasswordResetEmailAsync(string toEmail, string resetUrl, string userName = "");
    
    /// <summary>
    /// Envía un email genérico
    /// </summary>
    Task SendEmailAsync(string toEmail, string subject, string htmlBody);
}
