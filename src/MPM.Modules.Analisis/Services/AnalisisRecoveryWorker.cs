using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using MPM.Modules.Analisis.Data;

namespace MPM.Modules.Analisis.Services;

/// <summary>
/// Recupera análisis huérfanos: workspaces que quedaron en estado "analizando" porque el
/// <see cref="Task.Run"/> fire-and-forget de <see cref="AnalisisBackgroundService"/> murió con
/// la instancia (reinicio, CPU throttling de Cloud Run) antes de completar el análisis (QA
/// BUG-002). No introduce una cola nueva: reutiliza el estado ya persistido de forma síncrona
/// por <c>AnalisisService.SolicitarAnalisisAsync</c> antes de encolar el análisis original.
/// </summary>
public class AnalisisRecoveryWorker(
    IServiceScopeFactory scopeFactory,
    IConfiguration config,
    ILogger<AnalisisRecoveryWorker> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var thresholdMinutes = config.GetValue<int?>("Analisis:RecoveryThresholdMinutes") ?? 5;
        var pollIntervalSeconds = config.GetValue<int?>("Analisis:RecoveryPollIntervalSeconds") ?? 60;

        logger.LogInformation(
            "AnalisisRecoveryWorker starting. Threshold: {Threshold}min, poll: {Interval}s",
            thresholdMinutes, pollIntervalSeconds);

        // Espera inicial para no competir con el arranque del resto de la app.
        await Task.Delay(TimeSpan.FromSeconds(30), stoppingToken);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await RecuperarHuerfanosAsync(TimeSpan.FromMinutes(thresholdMinutes), stoppingToken);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Error en ciclo de AnalisisRecoveryWorker");
            }

            try
            {
                await Task.Delay(TimeSpan.FromSeconds(pollIntervalSeconds), stoppingToken);
            }
            catch (OperationCanceledException)
            {
                break;
            }
        }
    }

    internal async Task RecuperarHuerfanosAsync(TimeSpan threshold, CancellationToken ct)
    {
        using var scope = scopeFactory.CreateScope();
        var handler = scope.ServiceProvider.GetRequiredService<AnalisisHandler>();
        var backgroundService = scope.ServiceProvider.GetRequiredService<IAnalisisBackgroundService>();

        var (candidatos, _) = await handler.ListarWorkspacesAsync(page: 1, pageSize: 200, search: null, estado: "analizando", ct);
        if (candidatos.Count == 0) return;

        var ahora = DateTime.UtcNow;

        foreach (var candidato in candidatos)
        {
            ct.ThrowIfCancellationRequested();

            var detalle = await handler.ObtenerWorkspaceAsync(candidato.Id, ct);
            if (detalle == null || detalle.Estado != "analizando") continue;

            // updated_at es "timestamp without time zone" con convención UTC (sesión de
            // Postgres configurada en UTC) — Npgsql lo devuelve como DateTime.Kind=Unspecified.
            // ToUniversalTime() interpretaría eso como hora LOCAL de la máquina y lo
            // desplazaría según el huso horario del contenedor/host, dando antigüedades
            // incorrectas; SpecifyKind solo re-etiqueta el valor ya-UTC sin desplazarlo.
            var updatedAtUtc = DateTime.SpecifyKind(detalle.UpdatedAt, DateTimeKind.Utc);
            var antiguedad = ahora - updatedAtUtc;
            if (antiguedad < threshold) continue; // aún puede estar procesándose de verdad

            var resultadoExistente = await handler.ObtenerResultadoPorWorkspaceAsync(candidato.Id, ct);
            if (resultadoExistente != null)
            {
                // Terminó pero el estado no se actualizó a "completado" — bug distinto, fuera de
                // alcance de esta corrección; se deja registrado para investigar aparte.
                logger.LogWarning(
                    "Workspace {WorkspaceId} tiene resultado {ResultadoId} pero sigue en 'analizando' — no se reprocesa, requiere revisión aparte.",
                    candidato.Id, resultadoExistente.Id);
                continue;
            }

            var documentos = (await handler.ListarDocumentosAsync(candidato.Id, ct)).ToList();
            var ultimoDocumento = documentos.OrderByDescending(d => d.CreatedAt).FirstOrDefault();
            if (ultimoDocumento == null)
            {
                logger.LogWarning("Workspace {WorkspaceId} huérfano en 'analizando' sin documentos — no se puede reprocesar.", candidato.Id);
                continue;
            }

            var docDetalle = await handler.ObtenerDocumentoAsync(ultimoDocumento.Id, ct);
            if (docDetalle == null) continue;

            logger.LogWarning(
                "Reintentando análisis huérfano: workspace {WorkspaceId}, documento {DocumentoId} (inactivo {Minutos:F0} min)",
                candidato.Id, docDetalle.Id, antiguedad.TotalMinutes);

            backgroundService.EnqueueAnalisis(candidato.Id, docDetalle.Id, docDetalle.NombreArchivo, docDetalle.RutaStorage);
        }
    }
}
