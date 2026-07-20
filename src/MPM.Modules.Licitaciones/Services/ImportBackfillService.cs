using Microsoft.Extensions.Logging;
using MPM.Modules.Licitaciones.Data;

namespace MPM.Modules.Licitaciones.Services;

/// <summary>
/// 029-fix-hallazgos-code-review-competidores-alertas (FR-010/US6, QA BUG-003): backfill de
/// licitaciones del import histórico masivo (<c>V094__Reset_Licitaciones_Y_Analisis_Para_Import.sql</c>,
/// cargadas vía <c>gcloud sql import csv</c>, no vía <c>usp_SyncEngine_MergeLicitaciones</c>) que
/// quedaron con <c>tipo</c> genérico ("Licitacion") y/o <c>organismo</c> no recuperado.
///
/// Dos pasos independientes:
/// 1. <see cref="BackfillTipoPorSufijoAsync"/> -- determinístico, reusa
///    <see cref="ApiMpService.ParseTipoDesdeCodigo"/> (misma lógica que ya usa el path de sync
///    normal, no una segunda implementación) para derivar el tipo del sufijo de
///    <c>codigo_externo</c>. No llama a ninguna API externa.
/// 2. <see cref="BackfillOrganismoAsync"/> -- reusa el mecanismo de enriquecimiento que
///    <see cref="LicitacionService.ObtenerPorCodigoAsync"/> ya dispara on-demand al abrir el
///    detalle de una licitación (llamada real a la API de Mercado Público), corrido como job
///    sobre el lote de candidatos en vez de depender de que cada uno se abra manualmente.
/// </summary>
public class ImportBackfillService(
    LicitacionHandler licitacionHandler,
    LicitacionService licitacionService,
    ILogger<ImportBackfillService> logger)
{
    public async Task<BackfillResultado> BackfillTipoPorSufijoAsync(int limite = 1000, CancellationToken ct = default)
    {
        var candidatos = (await licitacionHandler.ListarParaBackfillTipoAsync(limite, ct)).ToList();
        var actualizados = 0;
        var noResueltos = new List<string>();

        foreach (var codigoExterno in candidatos)
        {
            var tipoDerivado = ApiMpService.ParseTipoDesdeCodigo(codigoExterno);
            if (tipoDerivado == "Licitacion")
            {
                // El sufijo no resolvió a nada mejor que el valor genérico -- no hay avance real,
                // se deja registrado para revisión en vez de "actualizar" con el mismo valor.
                noResueltos.Add(codigoExterno);
                continue;
            }

            await licitacionHandler.ActualizarTipoBackfillAsync(codigoExterno, tipoDerivado, ct);
            actualizados++;
        }

        if (noResueltos.Count > 0)
        {
            logger.LogWarning(
                "Backfill de tipo: {Count} licitación(es) sin sufijo reconocible en codigo_externo, quedan sin resolver: {Codigos}",
                noResueltos.Count, string.Join(", ", noResueltos));
        }

        logger.LogInformation("Backfill de tipo completado: {Actualizados} actualizados, {SinResolver} sin resolver, {Total} candidatos procesados",
            actualizados, noResueltos.Count, candidatos.Count);

        return new BackfillResultado(candidatos.Count, actualizados, noResueltos);
    }

    public async Task<BackfillResultado> BackfillOrganismoAsync(int limite = 100, CancellationToken ct = default)
    {
        var candidatos = (await licitacionHandler.ListarParaBackfillOrganismoAsync(limite, ct)).ToList();
        var actualizados = 0;
        var noResueltos = new List<string>();

        foreach (var codigoExterno in candidatos)
        {
            // ObtenerPorCodigoAsync ya contiene el mecanismo completo (llamar a la API real,
            // mapear, persistir) -- acá solo se dispara sobre el lote en vez de esperar a que
            // un usuario abra cada detalle manualmente.
            var detalle = await licitacionService.ObtenerPorCodigoAsync(codigoExterno, ct);
            if (detalle != null && !string.IsNullOrEmpty(detalle.Organismo))
            {
                actualizados++;
            }
            else
            {
                noResueltos.Add(codigoExterno);
            }
        }

        if (noResueltos.Count > 0)
        {
            logger.LogWarning(
                "Backfill de organismo: {Count} licitación(es) no recuperables desde la API real de Mercado Público: {Codigos}",
                noResueltos.Count, string.Join(", ", noResueltos));
        }

        logger.LogInformation("Backfill de organismo completado: {Actualizados} actualizados, {SinResolver} sin resolver, {Total} candidatos procesados",
            actualizados, noResueltos.Count, candidatos.Count);

        return new BackfillResultado(candidatos.Count, actualizados, noResueltos);
    }
}

public record BackfillResultado(int Candidatos, int Actualizados, List<string> NoResueltos);
