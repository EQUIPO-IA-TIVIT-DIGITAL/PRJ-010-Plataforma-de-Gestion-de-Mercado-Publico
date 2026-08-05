using System.Diagnostics;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using MPM.Modules.Competidores.Data;
using MPM.Modules.Competidores.Models;

namespace MPM.Modules.Competidores.Services;

/// <summary>
/// US4 (spec 031): mismo patrón get-or-generate que <see cref="CompetidorAnalysisService"/>,
/// pero en vez de llamar a Gemini in-process, encola un scrape acotado (competidor-mercado.js)
/// que corre en background y escribe el resultado final directo a Postgres -- ver
/// research.md §4 y contracts/competidores-actividad-mercado.md. El request HTTP nunca espera
/// al scraper: responde "generando" y el frontend hace polling.
/// </summary>
public class CompetidorMercadoService(
    ILogger<CompetidorMercadoService> logger,
    IConfiguration config,
    CompetidoresActividadMercadoHandler handler)
{
    private const string NodeBinary = "node";

    public async Task<ActividadMercadoResponse> ObtenerOGenerarAsync(
        string nombreCompetidor, ActividadMercadoRequest request, CancellationToken ct = default)
    {
        var cache = await handler.ObtenerCacheAsync(nombreCompetidor, request.Area, request.FechaDesde, request.FechaHasta, ct);

        if (cache != null && cache.Estado == "listo")
        {
            return new ActividadMercadoResponse(
                "listo", nombreCompetidor, cache.CantidadLicitaciones, cache.MontoTotalAdjudicado,
                cache.ContenidoJson != null ? System.Text.Json.JsonSerializer.Deserialize<object>(cache.ContenidoJson) : null);
        }

        if (cache != null && cache.Estado == "generando")
        {
            // ya encolado -- no se vuelve a disparar el scraper (idempotente, evita duplicar costo)
            return new ActividadMercadoResponse("generando", nombreCompetidor, null, null, null);
        }

        await handler.EncolarAsync(nombreCompetidor, request.Area, request.FechaDesde, request.FechaHasta, ct);
        await DispararScrapingAsync(nombreCompetidor, request, ct);

        return new ActividadMercadoResponse("generando", nombreCompetidor, null, null, null);
    }

    private async Task DispararScrapingAsync(string nombreCompetidor, ActividadMercadoRequest request, CancellationToken ct)
    {
        var palabrasClave = request.Area is { } area
            ? await handler.ObtenerPalabrasClaveAreaAsync(area, ct)
            : new[] { nombreCompetidor };

        var scriptPath = config["Scraper:CompetidorMercadoScriptPath"]
            ?? Path.Combine(AppContext.BaseDirectory, "tools", "competidor-mercado.js");

        var args = string.Join(' ', new[]
        {
            $"\"{scriptPath}\"",
            $"--competidor=\"{nombreCompetidor}\"",
            request.Area is { } a ? $"--area={a}" : "",
            $"--fechaDesde={request.FechaDesde:yyyy-MM-dd}",
            $"--fechaHasta={request.FechaHasta:yyyy-MM-dd}",
            $"--palabrasClave=\"{string.Join(',', palabrasClave)}\"",
        }.Where(s => !string.IsNullOrEmpty(s)));

        var startInfo = new ProcessStartInfo
        {
            FileName = NodeBinary,
            Arguments = args,
            WorkingDirectory = Path.GetDirectoryName(scriptPath),
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true,
        };

        // Mismas credenciales de DB que usa el scraper diario (ScraperBackgroundService) --
        // este proceso escribe directo a competidores_actividad_mercado, no vuelve a llamar a
        // la API. No necesita MP_RUT/MP_PASSWORD: la búsqueda pública y el Cuadro de Ofertas no
        // requieren sesión (confirmado en vivo, ver research.md §4).
        startInfo.EnvironmentVariables["DB_HOST"] = config["DB_HOST"] ?? "db";
        startInfo.EnvironmentVariables["DB_PORT"] = config["DB_PORT"] ?? "5432";
        startInfo.EnvironmentVariables["DB_NAME"] = config["DB_NAME"] ?? "";
        startInfo.EnvironmentVariables["DB_USER"] = config["DB_USER"] ?? "";
        startInfo.EnvironmentVariables["DB_PASSWORD"] = config["DB_PASSWORD"] ?? "";
        startInfo.EnvironmentVariables["DB_SSL"] = config["DB_SSL"] ?? "";

        logger.LogInformation(
            "Encolando actividad de mercado para {Competidor} (área {Area}, {Desde}-{Hasta})",
            nombreCompetidor, request.Area, request.FechaDesde, request.FechaHasta);

        try
        {
            var process = new Process { StartInfo = startInfo };
            process.Start();
            // Fire-and-forget deliberado: el resultado se persiste directo en Postgres por el
            // script Node, el request HTTP ya respondió "generando" -- no se espera Exited aquí.
        }
        catch (Exception ex)
        {
            // La fila ya quedó en 'generando' (EncolarAsync se llamó antes de invocar este
            // método) -- si el proceso nunca arrancó, se queda "generando" para siempre sin
            // reintento automático. Aceptable para v1: el usuario puede volver a pedir el
            // informe, lo que sí reintentará porque el estado seguirá siendo distinto de 'listo'.
            logger.LogError(ex, "No se pudo iniciar el proceso de actividad de mercado para {Competidor}", nombreCompetidor);
        }
    }
}
