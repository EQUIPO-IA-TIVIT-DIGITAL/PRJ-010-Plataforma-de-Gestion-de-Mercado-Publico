using System.Net.Mail;
using System.Text.Json;
using System.Text.RegularExpressions;
using MPM.Modules.Notificaciones.Services;
using MPM.Modules.Propuestas.Data;
using MPM.Modules.Propuestas.Models;

namespace MPM.Modules.Propuestas.Services;

public interface IDecisionAvisoNotifier
{
    Task<string?> CrearAsync(
        string destinatario, string codigoExterno, string decision, string loteId,
        CancellationToken ct = default);
}

public sealed class DecisionAvisoNotifier(NotificacionesService notificaciones) : IDecisionAvisoNotifier
{
    public async Task<string?> CrearAsync(
        string destinatario, string codigoExterno, string decision, string loteId,
        CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();
        var (id, error) = await notificaciones.CrearAsync(
            destinatario,
            NotificacionesService.DecisionAvisadaTipo,
            $"Decisión {decision.ToUpperInvariant()} para {codigoExterno}",
            $"Se registró una decisión {decision.ToUpperInvariant()} para la licitación {codigoExterno}.",
            new
            {
                codigoExterno,
                decision,
                loteId,
                destinatario,
            });

        return error ?? (id > 0 ? null : "No se pudo crear la notificación");
    }
}

public interface IDecisionAvisoService
{
    Task<AvisarResponse> AvisarAsync(
        string codigoExterno, long decisionId, IReadOnlyCollection<string>? destinatarios,
        CancellationToken ct = default);
}

public sealed class DecisionAvisoService(
    PropuestasHandler handler,
    IProposalLicitacionLookup licitacionLookup,
    IDecisionAvisoNotifier notifier) : IDecisionAvisoService
{
    private static readonly Regex EmailPattern = new(
        @"^[^\s@]+@[^\s@]+\.[^\s@]+$",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);

    public async Task<AvisarResponse> AvisarAsync(
        string codigoExterno, long decisionId, IReadOnlyCollection<string>? destinatarios,
        CancellationToken ct = default)
    {
        ValidateCodigo(codigoExterno);
        var emails = NormalizeDestinatarios(destinatarios);
        var licitacion = await licitacionLookup.ObtenerPorCodigoAsync(codigoExterno, ct)
            ?? throw new PropuestaService.PropuestaException("LIC_001", "Licitación no encontrada");

        var decision = await handler.ObtenerDecisionAsync(licitacion.Id, ct);
        if (decision == null || decision.Decision is not ("go" or "no_go"))
            throw new PropuestaService.PropuestaException("PRO_011", "No existe una decisión GO/NO GO para la licitación");
        if (decision.Id != decisionId)
            throw new PropuestaService.PropuestaException("PRO_012", "La decisión no corresponde a la licitación");

        var loteId = Guid.NewGuid().ToString("N");
        var enviados = 0;
        var fallos = 0;
        foreach (var email in emails)
        {
            try
            {
                var error = await notifier.CrearAsync(email, codigoExterno, decision.Decision, loteId, ct);
                if (!string.IsNullOrWhiteSpace(error))
                    throw new InvalidOperationException(error);
                enviados++;
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception)
            {
                fallos++;
            }
        }

        if (fallos > 0)
            throw new PropuestaService.PropuestaException(
                "PRO_012",
                $"No se completó el lote de avisos ({enviados} enviados, {fallos} con error); la decisión se conservó y la lista de notificados no se actualizó.");

        var json = JsonSerializer.Serialize(emails);
        try
        {
            await handler.ActualizarDecisionNotificadosAsync(decisionId, json, ct);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            throw new PropuestaService.PropuestaException(
                "PRO_012",
                "Los avisos fueron creados, pero no se pudo persistir el resultado del lote.",
                ex);
        }

        return new AvisarResponse
        {
            DecisionId = decisionId,
            CodigoExterno = codigoExterno,
            Decision = decision.Decision,
            Notificados = emails,
            NotificadoAt = DateTime.UtcNow,
            Enviados = emails.Count,
        };
    }

    private static List<string> NormalizeDestinatarios(IReadOnlyCollection<string>? values)
    {
        if (values == null || values.Count == 0)
            throw new PropuestaService.PropuestaException("PRO_007", "Debe seleccionar al menos un destinatario");
        if (values.Count > 50)
            throw new PropuestaService.PropuestaException("PRO_007", "No se pueden avisar más de 50 destinatarios");

        var result = new List<string>(values.Count);
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var value in values)
        {
            var email = value?.Trim() ?? string.Empty;
            try
            {
                _ = new MailAddress(email);
            }
            catch (FormatException)
            {
                throw new PropuestaService.PropuestaException("PRO_007", "La lista contiene un email inválido");
            }

            if (!EmailPattern.IsMatch(email) || !seen.Add(email))
                throw new PropuestaService.PropuestaException("PRO_007", "La lista contiene un email inválido o duplicado");
            result.Add(email);
        }

        return result;
    }

    private static void ValidateCodigo(string codigoExterno)
    {
        if (string.IsNullOrWhiteSpace(codigoExterno) || codigoExterno.Contains("..", StringComparison.Ordinal)
            || codigoExterno.Contains('/') || codigoExterno.Contains('\\'))
            throw new PropuestaService.PropuestaException("VAL_001", "codigoExterno inválido");
    }
}
