using System.Text.Json;
using MPM.Modules.Alertas.Models;
using MPM.Modules.Analisis.Data;

namespace MPM.Modules.Alertas.Services;

/// <summary>
/// Genera el resumen enriquecido de una licitación disparada (User Story 4).
///
/// T029 (2026-07-07): si ya existe un análisis de Gemini completado para esta licitación
/// (módulo Analisis, típicamente porque TIVIT participó y el scraper la procesó), se
/// reutiliza para completar requisitos/competidores/monto. La mayoría de licitaciones nuevas
/// nunca pasaron por Gemini, así que este es el caso poco común, no el default.
///
/// <c>FormaPago</c> y <c>Multas</c> quedan explícitamente en null siempre — el esquema de
/// extracción de <c>GeminiService</c> (módulo Analisis) no captura esos dos campos hoy;
/// agregarlos requeriría extender el prompt de análisis, fuera de este alcance (decisión
/// tomada con el usuario 2026-07-07, ver specs/003-fase6-alertas-keywords/tasks.md).
/// </summary>
public class AlertaEnriquecimientoService(AnalisisHandler analisisHandler)
{
    private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNameCaseInsensitive = true };

    public async Task<ResumenEnriquecido> GenerarAsync(LicitacionParaMatching licitacion, CancellationToken ct = default)
    {
        var presupuesto = licitacion.Monto.HasValue
            ? licitacion.Monto.Value.ToString("N0")
            : null;

        var esRenovacion = ContieneSenalDeRenovacion(licitacion.Descripcion);

        string? requisitos = null;
        string? competidores = null;

        var resultado = await analisisHandler.ObtenerResultadoPorLicitacionAsync(licitacion.LicitacionId, ct);
        if (!string.IsNullOrEmpty(resultado?.ContenidoJson))
        {
            (requisitos, competidores, presupuesto) = ExtraerDeAnalisis(resultado.ContenidoJson, presupuesto);
        }

        var resumen = new ResumenEnriquecido(
            Requisitos: requisitos,
            Competidores: competidores,
            Presupuesto: presupuesto,
            FormaPago: null,
            Multas: null,
            EsRenovacion: esRenovacion,
            ProveedorActual: null);

        return resumen;
    }

    private static (string? Requisitos, string? Competidores, string? Presupuesto) ExtraerDeAnalisis(string contenidoJson, string? presupuestoActual)
    {
        try
        {
            using var doc = JsonDocument.Parse(contenidoJson);
            var root = doc.RootElement;

            string? requisitos = null;
            if (root.TryGetProperty("requisitos", out var req))
                requisitos = req.GetRawText();

            string? competidores = null;
            if (root.TryGetProperty("adjudicacion", out var adj) && adj.TryGetProperty("ofertantes", out var ofertantes))
                competidores = ofertantes.GetRawText();

            var presupuesto = presupuestoActual;
            if (root.TryGetProperty("analisis_tivit", out var tivit) && tivit.TryGetProperty("monto_ofertado", out var monto)
                && monto.ValueKind is JsonValueKind.Number or JsonValueKind.String)
            {
                presupuesto ??= monto.ToString();
            }

            return (requisitos, competidores, presupuesto);
        }
        catch (JsonException)
        {
            // contenido_json corrupto/inesperado: no romper el resumen, solo no enriquecerlo.
            return (null, null, presupuestoActual);
        }
    }

    private static bool? ContieneSenalDeRenovacion(string? descripcion)
    {
        if (string.IsNullOrWhiteSpace(descripcion)) return null;
        var texto = descripcion.ToLowerInvariant();
        return texto.Contains("renovaci") || texto.Contains("proveedor actual") || texto.Contains("continuidad del servicio");
    }
}
