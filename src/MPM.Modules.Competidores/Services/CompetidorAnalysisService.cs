using System.Text.Json;
using MPM.Modules.Competidores.Data;
using MPM.Modules.Competidores.Models;
using MPM.Shared.Services;

namespace MPM.Modules.Competidores.Services;

public class CompetidorAnalysisService(
    OfertasHandler ofertasHandler,
    CompetidorAnalisisHandler analisisHandler,
    CompetidorGeminiService geminiService)
{
    public Task<IEnumerable<string>> ListarCompetidoresAsync(CancellationToken ct = default) =>
        ofertasHandler.ListarCompetidoresAsync(ct);

    public Task<IEnumerable<OfertaDto>> BuscarOfertasAsync(string nombreCompetidor, CancellationToken ct = default) =>
        ofertasHandler.BuscarPorCompetidorAsync(nombreCompetidor, ct);

    /// <summary>Llamado por el scraper (cuadroOfertas.js) para persistir las ofertas extraídas de una licitación.</summary>
    public async Task GuardarOfertasAsync(long licitacionId, IEnumerable<GuardarOfertaRequest> ofertas, CancellationToken ct = default)
    {
        foreach (var oferta in ofertas)
        {
            await ofertasHandler.GuardarAsync(licitacionId, oferta.RutProveedor, oferta.NombreProveedor, oferta.MontoOferta, oferta.EstadoOferta, ct);
        }
    }

    /// <summary>
    /// FR-004/FR-005/FR-006: nunca dispara Gemini sin confirmación explícita. Si ya existe un
    /// análisis cacheado para el mismo competidor+rango exacto, lo devuelve sin generar uno
    /// nuevo. Si no existe y `confirmar` es false, solo devuelve el conteo de licitaciones que
    /// entrarían, para que el usuario decida antes de gastar tokens de Gemini.
    /// </summary>
    /// <returns>
    /// 029-fix-hallazgos-code-review-competidores-alertas (FR-003/US3, QA relacionado con el
    /// límite de tokens): si Gemini bloquea el contenido (<see cref="GeminiRespuestaBloqueadaException"/>,
    /// ver <see cref="VertexGeminiClient"/>), <c>Resultado</c> es null y <c>ErrorCode</c> es
    /// <c>"gemini_contenido_bloqueado"</c> -- no se guarda ningún análisis parcial, el caller
    /// puede reintentar limpiamente. El controller traduce esto a un 422, no a un 500.
    /// </returns>
    public async Task<(AnalisisCompetidorResponse? Resultado, string? ErrorCode)> ObtenerOGenerarAnalisisAsync(
        AnalizarCompetidorRequest request, string usuarioId, CancellationToken ct = default)
    {
        var cacheado = await analisisHandler.BuscarCacheadoAsync(request.NombreCompetidor, request.FechaDesde, request.FechaHasta, ct);
        if (cacheado != null)
        {
            return (new AnalisisCompetidorResponse(
                Cacheado: true,
                CantidadLicitaciones: cacheado.Value.CantidadLicitaciones,
                Contenido: JsonSerializer.Deserialize<object>(cacheado.Value.Contenido.RootElement.GetRawText()),
                RequiereConfirmacion: false), null);
        }

        var cantidad = await ofertasHandler.ContarPorCompetidorYRangoAsync(request.NombreCompetidor, request.FechaDesde, request.FechaHasta, ct);

        if (!request.Confirmar)
        {
            // FR-006: mostrar el volumen antes de gastar en Gemini -- no se genera nada todavía.
            return (new AnalisisCompetidorResponse(Cacheado: false, CantidadLicitaciones: cantidad, Contenido: null, RequiereConfirmacion: true), null);
        }

        var ofertas = await ofertasHandler.ListarParaAnalisisAsync(request.NombreCompetidor, request.FechaDesde, request.FechaHasta, ct);
        var resumen = string.Join("\n", ofertas.Select(o =>
            $"- {o.CodigoExterno} | {o.NombreLicitacion} | {o.Organismo} | Monto: {o.MontoOferta} | Estado: {o.EstadoOferta}"));

        string contenidoJson;
        try
        {
            contenidoJson = await geminiService.AnalizarCompetidorAsync(request.NombreCompetidor, resumen, ct);
        }
        catch (GeminiRespuestaBloqueadaException)
        {
            return (null, "gemini_contenido_bloqueado");
        }

        await analisisHandler.GuardarAsync(request.NombreCompetidor, request.FechaDesde, request.FechaHasta, contenidoJson, cantidad, usuarioId, ct);

        // Relee tras guardar (en vez de devolver contenidoJson directo) por si otro usuario ganó
        // la carrera del ON CONFLICT DO NOTHING (edge case de concurrencia, research.md R5) --
        // así siempre se devuelve la versión que realmente quedó persistida.
        var final = await analisisHandler.BuscarCacheadoAsync(request.NombreCompetidor, request.FechaDesde, request.FechaHasta, ct)
            ?? throw new InvalidOperationException("El análisis se generó pero no se pudo releer tras guardarlo.");

        return (new AnalisisCompetidorResponse(
            Cacheado: false,
            CantidadLicitaciones: final.CantidadLicitaciones,
            Contenido: JsonSerializer.Deserialize<object>(final.Contenido.RootElement.GetRawText()),
            RequiereConfirmacion: false), null);
    }
}
