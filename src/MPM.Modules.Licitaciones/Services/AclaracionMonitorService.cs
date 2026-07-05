using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using MPM.Modules.Licitaciones.Data;
using MPM.Modules.Notificaciones.Services;

namespace MPM.Modules.Licitaciones.Services;

public class AclaracionMonitorService(
    ILogger<AclaracionMonitorService> logger,
    IConfiguration config,
    IServiceScopeFactory scopeFactory) : BackgroundService
{
    private static readonly int[] EstadosActivos = [1, 2, 4];

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var enabled = config.GetValue<bool>("Monitor:Enabled");
        if (!enabled && !config.GetValue<bool>("MONITOR_ENABLED"))
        {
            logger.LogInformation("AclaracionMonitorService disabled");
            return;
        }

        var intervalMinutes = config.GetValue<int>("Monitor:IntervalMinutes");
        if (intervalMinutes <= 0)
            intervalMinutes = config.GetValue<int>("MONITOR_INTERVAL_MINUTES");
        if (intervalMinutes <= 0) intervalMinutes = 30;

        logger.LogInformation("AclaracionMonitorService starting. Interval: {Min}m", intervalMinutes);

        // Wait 60s at startup before first cycle
        await Task.Delay(TimeSpan.FromSeconds(60), stoppingToken);

        while (!stoppingToken.IsCancellationRequested)
        {
            await EjecutarCicloAsync(stoppingToken);
            await Task.Delay(TimeSpan.FromMinutes(intervalMinutes), stoppingToken);
        }
    }

    private async Task EjecutarCicloAsync(CancellationToken ct)
    {
        try
        {
            logger.LogInformation("Monitor cycle triggered at {Time}", DateTime.UtcNow);

            var ticket = config["MP_TICKET"];
            if (string.IsNullOrWhiteSpace(ticket))
            {
                logger.LogWarning("MP_TICKET not configured — skipping aclaracion monitor cycle");
                return;
            }

            using var scope = scopeFactory.CreateScope();
            var seguimientoHandler = scope.ServiceProvider.GetRequiredService<SeguimientoHandler>();
            var apiMpService = scope.ServiceProvider.GetRequiredService<ApiMpService>();
            var notificacionesService = scope.ServiceProvider.GetRequiredService<NotificacionesService>();

            var licitaciones = (await seguimientoHandler.ObtenerParaMonitorAsync(EstadosActivos, ct)).ToList();

            if (licitaciones.Count == 0)
            {
                logger.LogInformation("Monitor cycle: no licitaciones seguidas activas");
                return;
            }

            logger.LogInformation("Monitor cycle: {N} licitaciones a revisar", licitaciones.Count);

            var totalNotificaciones = 0;

            foreach (var lic in licitaciones)
            {
                if (ct.IsCancellationRequested) break;

                try
                {
                    var detalle = await apiMpService.GetDetalleAsync(lic.CodigoExterno, ticket, ct);
                    if (detalle?.Preguntas?.Listado == null || detalle.Preguntas.Listado.Count == 0)
                    {
                        await Task.Delay(1000, ct);
                        continue;
                    }

                    foreach (var aclaracion in detalle.Preguntas.Listado)
                    {
                        DateTime? fechaPub = DateTime.TryParse(aclaracion.FechaPublicacion, out var fp) ? fp : null;
                        DateTime? fechaResp = DateTime.TryParse(aclaracion.FechaRespuesta, out var fr) ? fr : null;

                        var (esNueva, aclaracionId) = await seguimientoHandler.AclaracionUpsertAsync(
                            lic.CodigoExterno,
                            aclaracion.CodigoAclaracion,
                            aclaracion.Pregunta,
                            aclaracion.Respuesta,
                            fechaPub,
                            fechaResp,
                            ct);

                        if (!esNueva) continue;

                        // Notificar a todos los usuarios que siguen esta licitación
                        var preguntaCorta = aclaracion.Pregunta != null && aclaracion.Pregunta.Length > 120
                            ? aclaracion.Pregunta[..120] + "..."
                            : aclaracion.Pregunta ?? "Sin texto";

                        var tienRespuesta = !string.IsNullOrWhiteSpace(aclaracion.Respuesta);
                        var titulo = $"Nueva aclaración — {lic.CodigoExterno}";
                        var mensaje = preguntaCorta + (tienRespuesta ? " (respuesta disponible)" : " (sin respuesta aún)");
                        var metadata = new
                        {
                            codigo_externo = lic.CodigoExterno,
                            nombre = lic.Nombre,
                            codigo_aclaracion = aclaracion.CodigoAclaracion,
                            tiene_respuesta = tienRespuesta,
                        };

                        foreach (var usuarioId in lic.UsuarioIds)
                        {
                            await notificacionesService.CrearAsync(
                                usuarioId,
                                "aclaracion_detectada",
                                titulo,
                                mensaje,
                                metadata);
                            totalNotificaciones++;
                        }

                        await seguimientoHandler.MarcarNotificadaAsync(aclaracionId, ct);
                    }
                }
                catch (Exception ex)
                {
                    logger.LogWarning(ex, "Error procesando licitacion {Codigo}", lic.CodigoExterno);
                }

                // Rate limit: 1 request per second to MP API
                await Task.Delay(1000, ct);
            }

            logger.LogInformation(
                "Monitor cycle completed: {N} licitaciones, {M} notificaciones enviadas",
                licitaciones.Count, totalNotificaciones);
        }
        catch (OperationCanceledException)
        {
            // Normal shutdown
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Monitor cycle failed");
        }
    }
}
