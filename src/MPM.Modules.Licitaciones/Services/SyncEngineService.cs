using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

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
            await syncService.ExecuteSyncAsync(DateTime.UtcNow.AddDays(-windowDays), stoppingToken, "AUTO");
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
