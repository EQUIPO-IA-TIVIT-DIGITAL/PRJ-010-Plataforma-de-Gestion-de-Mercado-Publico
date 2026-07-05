using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using MPM.Modules.Licitaciones.Data;
using MPM.Modules.Licitaciones.Models;
using MPM.Modules.Licitaciones.Services;
using System.Text.Json;

namespace MPM.Modules.Licitaciones.Services;

public class SyncService(
    ILogger<SyncService> logger,
    IConfiguration config,
    ApiMpService apiMpService,
    SyncEngineHandler syncEngineHandler,
    SyncLogHandler syncLogHandler)
{
    public async Task<SyncStatusDto> ExecuteSyncAsync(DateTime? desde = null, CancellationToken ct = default, string tipo = "MANUAL")
    {
        var ticket = config["MP_TICKET"]
            ?? throw new InvalidOperationException("MP_TICKET not configured");

        logger.LogInformation("Iniciando sincronizacion ({Tipo})...", tipo);

        var syncId = await syncLogHandler.IniciarSync(tipo, ct);
        var createdTotal = 0;
        var updatedTotal = 0;
        var errorsTotal = 0;
        var errorDetails = new List<string>();

        var startDate = desde ?? DateTime.UtcNow.AddDays(-7);
        var endDate = DateTime.UtcNow;

        for (var date = startDate; date <= endDate; date = date.AddDays(1))
        {
            if (ct.IsCancellationRequested)
            {
                logger.LogInformation("Sincronizacion cancelada en {Date}", date.ToString("yyyy-MM-dd"));
                break;
            }

            var retries = 0;
            const int maxRetries = 3;

            while (retries <= maxRetries)
            {
                try
                {
                    var licitaciones = await apiMpService.GetLicitacionesDelDiaAsync(date, ticket, ct);
                    if (licitaciones.Count > 0)
                    {
                        var (creados, actualizados) = await syncEngineHandler.MergeLicitacionesAsync(licitaciones, ct);
                        createdTotal += creados;
                        updatedTotal += actualizados;
                        logger.LogInformation("Dia {Date}: {Count} registros ({Creados} nuevos, {Actualizados} actualizados)",
                            date.ToString("yyyy-MM-dd"), licitaciones.Count, creados, actualizados);
                    }
                    else
                    {
                        logger.LogInformation("Dia {Date}: sin datos", date.ToString("yyyy-MM-dd"));
                    }
                    break;
                }
                catch (HttpRequestException ex) when (ex.Message.Contains("429"))
                {
                    retries++;
                    if (retries > maxRetries)
                    {
                        errorsTotal++;
                        errorDetails.Add($"{date:yyyy-MM-dd}: rate limit tras {maxRetries} reintentos");
                        logger.LogWarning("Dia {Date}: rate limit maximo alcanzado", date.ToString("yyyy-MM-dd"));
                    }
                    else
                    {
                        var delay = retries * 3000;
                        logger.LogWarning("Dia {Date}: rate limit, reintento {Retry}/{Max} en {Delay}s",
                            date.ToString("yyyy-MM-dd"), retries, maxRetries, delay / 1000);
                        await Task.Delay(delay, ct);
                    }
                }
                catch (Exception ex)
                {
                    errorsTotal++;
                    errorDetails.Add($"{date:yyyy-MM-dd}: {ex.Message}");
                    logger.LogError(ex, "Error en dia {Date}", date.ToString("yyyy-MM-dd"));
                    break;
                }
            }

            try
            {
                await Task.Delay(2000, ct);
            }
            catch (OperationCanceledException)
            {
                logger.LogInformation("Sincronizacion cancelada en {Date}", date.ToString("yyyy-MM-dd"));
                break;
            }
        }

        var detalleErrores = errorsTotal > 0 ? JsonSerializer.Serialize(errorDetails) : null;

        await syncLogHandler.FinalizarSync(syncId, createdTotal, updatedTotal, 0, errorsTotal, detalleErrores, ct);

        logger.LogInformation("Sincronizacion completa: {Creados} creados, {Actualizados} actualizados, {Errores} errores",
            createdTotal, updatedTotal, errorsTotal);

        return new SyncStatusDto
        {
            SyncId = (int)syncId,
            Status = errorsTotal > 0 && createdTotal == 0 && updatedTotal == 0 ? "FALLO" : "COMPLETADO",
            StartedAt = DateTime.UtcNow
        };
    }
}
