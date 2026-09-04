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

    // V138 (fix "actividad total de mercado falla siempre"): una fila en 'generando' con mas
    // de este tiempo sin actualizarse se considera estancada (el scraper nunca arrancó o murió)
    // y se reintenta en vez de dejar al usuario polleando para siempre.
    private static readonly TimeSpan StaleGenerandoThreshold = TimeSpan.FromMinutes(10);

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

        if (cache != null && cache.Estado == "generando" && EsGenerandoVivo(cache))
        {
            // ya encolado y en curso -- no se vuelve a disparar el scraper (idempotente, evita duplicar costo)
            return new ActividadMercadoResponse("generando", nombreCompetidor, null, null, null);
        }

        // cache null, 'error', o 'generando' estancado: re-encolar y reintentar.
        await handler.EncolarAsync(nombreCompetidor, request.Area, request.FechaDesde, request.FechaHasta, ct);
        var arranco = await DispararScrapingAsync(nombreCompetidor, request, ct);

        if (!arranco)
        {
            // V138: si el proceso no arranca (script no publicado, node ausente, etc.) la fila
            // queda en 'error' en vez de 'generando' para siempre -- el frontend muestra el
            // error con botón de reintento, y el próximo request reintentará igualmente.
            await handler.MarcarErrorAsync(nombreCompetidor, request.Area, request.FechaDesde, request.FechaHasta, ct);
        }

        return new ActividadMercadoResponse("generando", nombreCompetidor, null, null, null);
    }

    private static bool EsGenerandoVivo(ActividadMercadoCacheRow cache)
    {
        if (cache.UpdatedAt is not { } updatedAt) return false;
        return DateTime.UtcNow - updatedAt.ToUniversalTime() < StaleGenerandoThreshold;
    }

    private async Task<bool> DispararScrapingAsync(string nombreCompetidor, ActividadMercadoRequest request, CancellationToken ct)
    {
        var palabrasClave = request.Area is { } area
            ? await handler.ObtenerPalabrasClaveAreaAsync(area, ct)
            : new[] { nombreCompetidor };

        var scriptPath = ResolverScriptPath();
        if (scriptPath == null || !File.Exists(scriptPath))
        {
            logger.LogError(
                "No se encontró el script de actividad de mercado en ninguna ruta conocida. " +
                "Configurar Scraper:CompetidorMercadoScriptPath (dev: ../tools/scraper-mp-v2/competidor-mercado.js; " +
                "Docker/Cloud Run: /app/tools/competidor-mercado.js)");
            return false;
        }

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
            "Encolando actividad de mercado para {Competidor} (área {Area}, {Desde}-{Hasta}) con script {Script}",
            nombreCompetidor, request.Area, request.FechaDesde, request.FechaHasta, scriptPath);

        try
        {
            var process = new Process { StartInfo = startInfo };
            process.Start();

            // V138: se espera solo lo necesario para detectar fallas inmediatas (script no
            // encontrado por node, módulo roto, DB inalcanzable al boot). Una corrida legítima
            // tarda minutos; si sigue viva tras 5s se la deja correr fire-and-forget (el
            // resultado se persiste directo en Postgres y el request HTTP ya respondió "generando").
            if (process.WaitForExit(5000) && process.ExitCode != 0)
            {
                var stderr = process.StandardError.ReadToEnd();
                logger.LogError(
                    "El proceso de actividad de mercado para {Competidor} salió inmediatamente con código {ExitCode}: {Stderr}",
                    nombreCompetidor, process.ExitCode, stderr);
                return false;
            }

            return true;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "No se pudo iniciar el proceso de actividad de mercado para {Competidor}", nombreCompetidor);
            return false;
        }
    }

    private string? ResolverScriptPath()
    {
        // 1) Config explícita (appsettings.json, env Scraper__CompetidorMercadoScriptPath).
        var configPath = config["Scraper:CompetidorMercadoScriptPath"];
        if (!string.IsNullOrWhiteSpace(configPath))
        {
            var full = Path.GetFullPath(configPath, AppContext.BaseDirectory);
            if (File.Exists(full)) return full;
            // Config relativa al directorio de trabajo (dotnet run desde src/MPM.Api):
            var cwdFull = Path.GetFullPath(configPath, Directory.GetCurrentDirectory());
            if (File.Exists(cwdFull)) return cwdFull;
            // Ya fue explicitamente configurada: si no existe, reportar (el log lo dirá) y
            // no intentar el fallback para no ocultar una config errónea.
            return null;
        }

        // 2) Fallback histórico: tools/ junto al binario publicado (Docker/Cloud Run).
        var publishPath = Path.Combine(AppContext.BaseDirectory, "tools", "competidor-mercado.js");
        if (File.Exists(publishPath)) return publishPath;

        // 3) Fallback repo: tools/scraper-mp-v2/ relativo al directorio de trabajo.
        var repoPath = Path.GetFullPath(Path.Combine("..", "tools", "scraper-mp-v2", "competidor-mercado.js"));
        if (File.Exists(repoPath)) return repoPath;

        return null;
    }
}
