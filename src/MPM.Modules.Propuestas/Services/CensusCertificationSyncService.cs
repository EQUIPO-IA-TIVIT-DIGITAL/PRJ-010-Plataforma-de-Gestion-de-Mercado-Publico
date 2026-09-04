using System.Diagnostics;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using MPM.Modules.Censo.Services;
using MPM.Modules.Propuestas.Data;
using MPM.Modules.Propuestas.Models;

namespace MPM.Modules.Propuestas.Services;

public class CensusCertificationSyncService(
    CensusClient censusClient,
    PropuestasHandler handler,
    IConfiguration configuration,
    ILogger<CensusCertificationSyncService> logger)
{
    private static readonly SemaphoreSlim SyncLock = new(1, 1);

    public class CensusPayloadTooLargeException(string message) : Exception(message);

    public virtual async Task<CensusSyncResultDto> SincronizarAsync(CancellationToken ct = default)
    {
        await SyncLock.WaitAsync(ct);
        try
        {
            var sw = Stopwatch.StartNew();
            var records = await censusClient.GetUserCertificationsAsync(ct);
            var maxRecords = configuration.GetValue<int?>("Propuestas:CensusSync:MaxRecords") ?? 10000;
            if (records.Count > maxRecords)
                throw new CensusPayloadTooLargeException($"El payload de Census excede el máximo configurado ({maxRecords})");

            var grouped = records
                .Where(r => !string.IsNullOrWhiteSpace(r.CertificationTypeName))
                .GroupBy(r => CertificationNameNormalizer.NormalizeKey(r.CertificationTypeName), StringComparer.Ordinal)
                .Select(group =>
                {
                    var first = group.First();
                    return new CertificationSyncItem(
                        CertificationNameNormalizer.NormalizeDisplay(first.CertificationTypeName),
                        group.Key,
                        group.Select(x => x.FileId).FirstOrDefault(x => !string.IsNullOrWhiteSpace(x)),
                        group.Select(x => x.Institution).FirstOrDefault(x => !string.IsNullOrWhiteSpace(x)),
                        group.Select(x => x.Validity).FirstOrDefault(x => !string.IsNullOrWhiteSpace(x)));
                })
                .ToList();

            var mutation = await handler.SincronizarCertificacionesAsync(grouped, ct);
            sw.Stop();
            logger.LogInformation("Sincronización de certificaciones Census completada: {Procesadas} registros, {Certificaciones} nombres, {DurationMs} ms", records.Count, grouped.Count, sw.ElapsedMilliseconds);
            return new CensusSyncResultDto { Procesadas = records.Count, Insertadas = mutation.Insertadas, Actualizadas = mutation.Actualizadas, SinArchivo = mutation.SinArchivo, DurationMs = sw.ElapsedMilliseconds };
        }
        finally { SyncLock.Release(); }
    }
}
