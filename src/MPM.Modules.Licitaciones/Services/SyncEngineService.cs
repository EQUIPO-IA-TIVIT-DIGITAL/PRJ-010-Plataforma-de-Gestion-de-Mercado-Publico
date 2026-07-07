using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using MPM.Modules.Alertas.Services;
using MPM.Modules.Licitaciones.Data;

namespace MPM.Modules.Licitaciones.Services;

public class SyncEngineService(
    ILogger<SyncEngineService> logger,
    IConfiguration config,
    IServiceProvider serviceProvider) : BackgroundService
{
    // Marca en sync_log que garantiza que el backfill histórico corre una sola vez
    internal const string BackfillTipo = "BACKFILL25";

    private Timer? _timer;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        logger.LogInformation("SyncEngineService starting.");

        var intervalDays = config.GetValue("Sync:IntervalDays", 7);
        var interval = TimeSpan.FromDays(intervalDays);

        await Task.Delay(TimeSpan.FromSeconds(10), stoppingToken);

        await Task.Run(() => DoWorkAsync(stoppingToken), stoppingToken);

        _timer = new Timer(DoWorkAsync, null, interval, interval);
        logger.LogInformation("Sync programado cada {Days} dias.", intervalDays);

        await Task.Delay(Timeout.Infinite, stoppingToken);
    }

    /// <summary>
    /// Ejecuta un solo ciclo de sync y retorna (sin Timer, sin loop infinito). Pensado para
    /// invocarse desde el "modo worker" de <c>Program.cs</c> (Cloud Run Job <c>sync-job</c>,
    /// ver 002-fase5-deploy-gcp plan.md T008-T009) — la lógica es la misma que usa el
    /// <see cref="BackgroundService"/> en desarrollo/Docker Compose local.
    /// </summary>
    public Task EjecutarCicloUnaVezAsync(CancellationToken ct = default) => DoWorkAsync(ct);

    private async void DoWorkAsync(object? state)
    {
        if (state is CancellationToken ct)
            await DoWorkAsync(ct);
        else
            await DoWorkAsync(CancellationToken.None);
    }

    private async Task DoWorkAsync(CancellationToken stoppingToken)
    {
        // El try/catch envuelve todo el ciclo: cualquier fallo queda logueado
        // (además del registro FALLO/PARCIAL en sync_log) y el timer sigue vivo
        try
        {
            logger.LogInformation("Sync cycle triggered at {Time}", DateTime.UtcNow);
            using var scope = serviceProvider.CreateScope();
            var syncService = scope.ServiceProvider.GetRequiredService<SyncService>();
            var syncLogHandler = scope.ServiceProvider.GetRequiredService<SyncLogHandler>();

            // Backfill one-shot: cubre el período 2025-2026 desde Sync:BackfillDesde
            // hasta hoy; idempotente vía la marca BACKFILL25 en sync_log
            var backfillPendiente = false;
            try
            {
                backfillPendiente = !await syncLogHandler.ExisteTipo(BackfillTipo, stoppingToken);
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "No se pudo verificar la marca de backfill; se ejecuta sync incremental");
            }

            if (backfillPendiente)
            {
                var backfillDesde = config.GetValue("Sync:BackfillDesde", new DateTime(2025, 1, 1));
                logger.LogInformation("Ejecutando backfill historico desde {Desde}", backfillDesde.ToString("yyyy-MM-dd"));
                await syncService.ExecuteSyncAsync(backfillDesde, stoppingToken, BackfillTipo);
                return;
            }

            // Incremental: ventana con 1 dia de solapamiento para no perder
            // licitaciones publicadas en el borde del ciclo anterior
            var windowDays = config.GetValue("Sync:WindowDays", 8);
            var desde = DateTime.UtcNow.AddDays(-windowDays);
            await syncService.ExecuteSyncAsync(desde, stoppingToken, "AUTO");

            // Motor de matching de Alertas (003-fase6-alertas-keywords): evalúa las
            // licitaciones de esta misma ventana contra las reglas activas. Solo en el ciclo
            // incremental — el backfill histórico (arriba) no debe disparar un aluvión de
            // alertas por licitaciones viejas.
            try
            {
                var licitacionHandler = scope.ServiceProvider.GetRequiredService<LicitacionHandler>();
                var alertasMatching = scope.ServiceProvider.GetRequiredService<AlertasMatchingService>();
                var licitaciones = await licitacionHandler.ListarParaMatchingAsync(desde, stoppingToken);
                await alertasMatching.EvaluarLicitacionesAsync(licitaciones, stoppingToken);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Fallo evaluando alertas tras el ciclo de sync (el sync en sí ya se completó)");
            }
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Sync cycle failed");
        }
    }

    public override Task StopAsync(CancellationToken cancellationToken)
    {
        logger.LogInformation("SyncEngineService stopping.");
        _timer?.Change(Timeout.Infinite, 0);
        return base.StopAsync(cancellationToken);
    }
}
