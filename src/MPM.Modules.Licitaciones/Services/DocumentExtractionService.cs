using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using MPM.Modules.Licitaciones.Data;
using MPM.Modules.Licitaciones.Models;

namespace MPM.Modules.Licitaciones.Services;

/// <summary>
/// Orquesta la extracción de documentos de una licitación según <c>Extraccion:Modo</c>
/// (research.md R5 de 016-extraccion-documentos-api):
/// <list type="bullet">
/// <item><c>solo_navegador</c> (default): no hace nada nuevo — el flujo actual
/// (<see cref="ScraperBackgroundService"/>, daemon Node independiente) sigue siendo la
/// única fuente. Este modo existe para poder desplegar el código de 016 sin cambiar
/// comportamiento en producción hasta activarlo explícitamente.</item>
/// <item><c>paralelo</c>: ejecuta también el extractor HTTP directo y registra el
/// resultado en <c>extraccion_documentos_log</c> para comparar cobertura, sin cambiar
/// qué se persiste como fuente de verdad.</item>
/// <item><c>directo_con_fallback</c>: el extractor HTTP directo es primario.</item>
/// </list>
///
/// ⚠️ Limitación conocida: el "fallback a navegador" de <c>directo_con_fallback</c> NO
/// implementa hoy una re-ejecución del navegador para una licitación puntual — el scraper
/// Node (<c>tools/scraper-mp/agente-mp.js</c>) solo sabe operar sobre el resultado de una
/// búsqueda completa (<c>buscarLicitaciones</c>), no navegar directo a la ficha de un
/// código arbitrario sin antes buscarlo (no se conoce el patrón de URL/token `enc` de la
/// ficha fuera de una búsqueda). Implementar ese fallback per-licitación es trabajo
/// adicional, no cubierto en esta pasada. Mientras tanto, si el extractor directo falla en
/// modo `directo_con_fallback`, se registra el fallo y esa licitación queda pendiente para
/// el próximo ciclo completo del daemon (que sí la cubre igual que hoy).
/// </summary>
public class DocumentExtractionService(
    ILogger<DocumentExtractionService> logger,
    IConfiguration config,
    AdjuntosHttpExtractor adjuntosHttpExtractor,
    ExtraccionLogHandler extraccionLogHandler)
{
    public async Task<ResultadoExtraccion> ExtraerAsync(LicitacionRef lic, CancellationToken ct = default)
    {
        var modo = config["Extraccion:Modo"] ?? "solo_navegador";

        if (await extraccionLogHandler.ExistePorLicitacionAsync(lic.LicitacionId, ct) && modo != "paralelo")
        {
            return new ResultadoExtraccion("navegador", "exito", 0, false, null, 0, false);
        }

        return modo switch
        {
            "paralelo" => await EjecutarParaleloAsync(lic, ct),
            "directo_con_fallback" => await EjecutarDirectoConFallbackAsync(lic, ct),
            _ => ResultadoNavegadorSinCambios(),
        };
    }

    private static ResultadoExtraccion ResultadoNavegadorSinCambios() =>
        new("navegador", "exito", 0, false, null, 0, false);

    private async Task<ResultadoExtraccion> EjecutarParaleloAsync(LicitacionRef lic, CancellationToken ct)
    {
        var resultadoDirecto = await adjuntosHttpExtractor.ExtraerAsync(lic, ct);
        await RegistrarLogAsync(lic, resultadoDirecto, ct);

        logger.LogInformation(
            "Modo paralelo — licitación {Codigo}: directo={Estado} ({Docs} docs, acta={Acta})",
            lic.CodigoExterno, resultadoDirecto.Estado, resultadoDirecto.DocumentosObtenidos, resultadoDirecto.ActaObtenida);

        // El navegador (daemon existente) sigue siendo la fuente de verdad en modo paralelo;
        // este resultado es solo para comparación (research.md R5/US3).
        return ResultadoNavegadorSinCambios();
    }

    private async Task<ResultadoExtraccion> EjecutarDirectoConFallbackAsync(LicitacionRef lic, CancellationToken ct)
    {
        var resultadoDirecto = await adjuntosHttpExtractor.ExtraerAsync(lic, ct);
        await RegistrarLogAsync(lic, resultadoDirecto, ct);

        if (resultadoDirecto.Estado is "exito" or "sin_adjuntos")
            return resultadoDirecto;

        logger.LogWarning(
            "Extracción directa falló para licitación {Codigo}: {Error}. Fallback per-licitación a navegador no implementado — queda pendiente para el próximo ciclo del daemon existente.",
            lic.CodigoExterno, resultadoDirecto.Error);

        var resultadoFallback = resultadoDirecto with { EsFallback = true };
        await RegistrarLogAsync(lic, resultadoFallback, ct);
        return resultadoFallback;
    }

    private Task RegistrarLogAsync(LicitacionRef lic, ResultadoExtraccion resultado, CancellationToken ct) =>
        extraccionLogHandler.RegistrarAsync(
            lic.LicitacionId, resultado.Metodo, resultado.Estado, resultado.DocumentosObtenidos,
            resultado.ActaObtenida, resultado.EsFallback, resultado.Error, resultado.DuracionMs, ct);
}
