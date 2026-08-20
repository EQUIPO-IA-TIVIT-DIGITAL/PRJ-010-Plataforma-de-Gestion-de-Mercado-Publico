using System.Collections.Concurrent;
using System.Diagnostics;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using MPM.Modules.Licitaciones.Data;
using MPM.Modules.Licitaciones.Models;
using MPM.Shared.Services;

namespace MPM.Modules.Licitaciones.Services;

/// <summary>
/// Descarga bajo demanda de los documentos de una licitación (036-flujo-comercial-ofertas,
/// spec docs/api-first/licitaciones-documentos.md).
///
/// Patrón get-or-generate (igual que CompetidorMercadoService): el request HTTP nunca espera
/// al scraper Node; responde "descargando" y el frontend hace polling a ObtenerEstadoAsync.
/// El script Node (tools/scraper-mp-v2/descargar-documentos.js) hace login con la sesión
/// existente, descarga TODOS los adjuntos, calcula SHA-256 y persiste (hash + versión + estado).
/// </summary>
public class AdjuntoDescargaService(
    ILogger<AdjuntoDescargaService> logger,
    IConfiguration config,
    AdjuntoDocumentosHandler handler,
    ExtraccionLogHandler extraccionLogHandler,
    IStorageService storageService)
{
    private const string NodeBinary = "node";
    private static readonly TimeSpan StaleDescargandoThreshold = TimeSpan.FromMinutes(10);
    private static readonly ConcurrentDictionary<long, DateTime> EnCurso = new();

    /// <summary>Excepción tipada: ya hay una extracción en curso para esta licitación.</summary>
    public class DescargaEnCursoException : Exception
    {
        public DescargaEnCursoException(string message) : base(message) { }
    }

    public async Task<EstadoDocumentosDto> ObtenerEstadoAsync(long licitacionId, CancellationToken ct = default)
    {
        var filas = await handler.ListarAsync(licitacionId, ct);

        var enMemoria = EnCurso.TryGetValue(licitacionId, out var iniciada) && (DateTime.UtcNow - iniciada) < StaleDescargandoThreshold;

        var estado = enMemoria
            ? "descargando"
            : filas.Count == 0
                ? "pendiente"
                : filas.Any(f => f.DescargaEstado == "descargando") ? "descargando"
                : filas.FirstOrDefault(f => f.DescargaEstado == "error") is { } err ? "error"
                : "completado";

        var error = filas.FirstOrDefault(f => f.DescargaEstado == "error")?.DescargaError;

        // Sin filas de adjuntos y no está en curso: consultar último log de extracción
        if (filas.Count == 0 && estado == "pendiente")
        {
            var ultima = await handler.ObtenerUltimaExtraccionAsync(licitacionId, ct);
            if (ultima is { Estado: "fallo" } && !string.IsNullOrWhiteSpace(ultima.Error))
            {
                estado = "error";
                error = ultima.Error;
            }
            else if (ultima is { Estado: "sin_adjuntos" } || ultima is { Estado: "exito", DocumentosObtenidos: 0 })
            {
                estado = "completado";
            }
        }

        return new EstadoDocumentosDto
        {
            EstadoConjunto = estado,
            DescargaError = error,
            ConjuntoHash = AdjuntoDocumentosHash.CalcularConjuntoHash(filas),
            Documentos = filas.Select(MapToDto).ToList(),
        };
    }

    /// <summary>
    /// Inicia (o reutiliza) la descarga de documentos. Si ya hay una extracción viva → responde
    /// "descargando" sin re-disparar (idempotente). Si el script no arranca → estado "error".
    /// </summary>
    public async Task<DescargarDocumentosResultDto> IniciarDescargaAsync(
        long licitacionId, string codigoExterno, string usuario, bool forzar, CancellationToken ct = default)
    {
        // Guard in-process: evita dobles clics simultáneos (DOC_006).
        if (!forzar && EnCurso.TryGetValue(licitacionId, out var inicio) && DateTime.UtcNow - inicio < StaleDescargandoThreshold)
            throw new DescargaEnCursoException("Ya hay una extracción de documentos en curso para esta licitación");

        try
        {
            // Idempotencia con estado persistido: si la extracción sigue viva (< 10 min), no re-disparar.
            if (!forzar && await handler.ExistenDescargasVivasAsync(licitacionId, ct))
            {
                var estadoActual = await ObtenerEstadoAsync(licitacionId, ct);
                return new DescargarDocumentosResultDto
                {
                    EstadoConjunto = "descargando",
                    Accion = "ya_en_curso",
                    ConjuntoHash = estadoActual.ConjuntoHash,
                };
            }

            if (await handler.MarcarDescargaIniciadaAsync(licitacionId, usuario, ct) is { Length: > 0 } errInit)
            {
                logger.LogError("No se pudo marcar inicio de descarga para licitación {LicitacionId}: {Error}", licitacionId, errInit);
                return ErrorResult("No se pudo iniciar la extracción (error interno)");
            }

            var scriptPath = ResolverScriptPath();
            if (scriptPath == null || !File.Exists(scriptPath))
            {
                logger.LogError(
                    "No se encontró descargar-documentos.js. Configurar Extraccion:ScriptDescargaPath " +
                    "(dev: ../tools/scraper-mp-v2/descargar-documentos.js; Docker/Cloud Run: /app/tools/descargar-documentos.js)");
                return await FallarAsync(licitacionId, "No se encontró el script de descarga de documentos", ct);
            }

            EnCurso[licitacionId] = DateTime.UtcNow;

            var arranco = DispararScraper(scriptPath, licitacionId, codigoExterno);
            if (!arranco)
            {
                EnCurso.TryRemove(licitacionId, out _);
                return await FallarAsync(licitacionId, "El proceso de descarga no arrancó (script/node no disponible)", ct);
            }

            return new DescargarDocumentosResultDto { EstadoConjunto = "descargando", Accion = "descargando" };
        }
        catch
        {
            EnCurso.TryRemove(licitacionId, out _);
            throw;
        }
    }

    /// <summary>Resuelve el stream del archivo guardado (GCS o local). Null si no existe.</summary>
    public async Task<ArchivoDocumentoResult?> ObtenerArchivoAsync(long licitacionId, long documentoId, CancellationToken ct = default)
    {
        var filas = await handler.ListarAsync(licitacionId, ct);
        var fila = filas.FirstOrDefault(f => f.Id == documentoId);
        if (fila == null) return null;

        // Prioridad: GCS (ruta gs://) → ruta local del scraper → ruta_storage como path local.
        if (fila.RutaStorage.StartsWith("gs://", StringComparison.OrdinalIgnoreCase))
        {
            var streamGcs = await storageService.DownloadAsync(fila.RutaStorage, ct);
            if (streamGcs != null)
                return new ArchivoDocumentoResult(streamGcs, fila.MimeType ?? "application/octet-stream", fila.NombreArchivo);
        }

        if (!string.IsNullOrWhiteSpace(fila.RutaLocal) && File.Exists(fila.RutaLocal))
        {
            var streamLocal = new FileStream(fila.RutaLocal, FileMode.Open, FileAccess.Read, FileShare.Read, 4096, useAsync: true);
            return new ArchivoDocumentoResult(streamLocal, fila.MimeType ?? "application/octet-stream", fila.NombreArchivo);
        }

        var streamStorage = await storageService.DownloadAsync(fila.RutaStorage, ct);
        return streamStorage == null
            ? null
            : new ArchivoDocumentoResult(streamStorage, fila.MimeType ?? "application/octet-stream", fila.NombreArchivo);
    }

    public record ArchivoDocumentoResult(Stream Stream, string MimeType, string NombreArchivo);

    private async Task<DescargarDocumentosResultDto> FallarAsync(long licitacionId, string motivo, CancellationToken ct)
    {
        await handler.MarcarDescargaFinalizadaAsync(licitacionId, "error", motivo, ct);
        await extraccionLogHandler.RegistrarAsync(
            licitacionId, "navegador", "fallo", 0, false, false, motivo, 0, ct);
        return ErrorResult(motivo);
    }

    private static DescargarDocumentosResultDto ErrorResult(string error) => new()
    {
        EstadoConjunto = "error",
        Accion = "error",
        Errores = 1,
        DescargaError = error,
    };

    private bool DispararScraper(string scriptPath, long licitacionId, string codigoExterno)
    {
        var args = $"\"{scriptPath}\" --codigo=\"{codigoExterno}\" --licitacionId={licitacionId}";

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

        // Xvfb: mismo patrón que ScraperBackgroundService — Mercado Público penaliza el
        // fingerprint headless de Chromium en "Ver Adjuntos" con reCAPTCHA (403 sistemático,
        // verificado en vivo 2026-07-06). En Cloud Run el Dockerfile instala xvfb-run; si está
        // disponible, envolver la invocación de Node con un framebuffer virtual y correr Chromium
        // en modo headed (MP_HEADLESS=false). En local (sin xvfb-run) cae al flujo headless.
        var useXvfb = IsXvfbAvailable();
        if (useXvfb)
        {
            startInfo.FileName = "xvfb-run";
            startInfo.Arguments = $"--auto-servernum -- {NodeBinary} {args}";
            logger.LogInformation("Xvfb detectado — descarga de documentos correrá en modo headed dentro de framebuffer virtual");
        }

        // Mismas credenciales de DB que el scraper diario + credenciales MP para login.
        startInfo.EnvironmentVariables["DB_HOST"] = config["DB_HOST"] ?? "db";
        startInfo.EnvironmentVariables["DB_PORT"] = config["DB_PORT"] ?? "5432";
        startInfo.EnvironmentVariables["DB_NAME"] = config["DB_NAME"] ?? "";
        startInfo.EnvironmentVariables["DB_USER"] = config["DB_USER"] ?? "";
        startInfo.EnvironmentVariables["DB_PASSWORD"] = config["DB_PASSWORD"] ?? "";
        startInfo.EnvironmentVariables["DB_SSL"] = config["DB_SSL"] ?? "";
        startInfo.EnvironmentVariables["MP_RUT"] = config["MP_RUT"] ?? "";
        startInfo.EnvironmentVariables["MP_PASSWORD"] = config["MP_PASSWORD"] ?? "";
        startInfo.EnvironmentVariables["MP_HEADLESS"] = useXvfb ? "false" : (config["MP_HEADLESS"] ?? "true");
        var adjuntosDir = config["Extraccion:AdjuntosDir"];
        startInfo.EnvironmentVariables["ADJUNTOS_DIR"] = string.IsNullOrWhiteSpace(adjuntosDir)
            ? Path.Combine(Directory.GetCurrentDirectory(), "uploads")
            : adjuntosDir;

        logger.LogInformation(
            "Disparando descarga de documentos para licitación {Codigo} (id={LicitacionId}) con script {Script}",
            codigoExterno, licitacionId, scriptPath);

        try
        {
            var process = new Process { StartInfo = startInfo, EnableRaisingEvents = true };

            process.OutputDataReceived += (s, e) =>
            {
                if (!string.IsNullOrWhiteSpace(e.Data))
                    logger.LogInformation("[SCRAPER-OUT] {Line}", e.Data);
            };

            process.ErrorDataReceived += (s, e) =>
            {
                if (!string.IsNullOrWhiteSpace(e.Data))
                    logger.LogError("[SCRAPER-ERR] {Line}", e.Data);
            };

            process.Exited += (s, e) =>
            {
                logger.LogInformation(
                    "Proceso scraper para licitación {Codigo} (id={LicitacionId}) terminó con ExitCode={ExitCode}",
                    codigoExterno, licitacionId, process.ExitCode);
                EnCurso.TryRemove(licitacionId, out _);
                process.Dispose();
            };

            var started = process.Start();
            if (!started)
            {
                EnCurso.TryRemove(licitacionId, out _);
                return false;
            }

            process.BeginOutputReadLine();
            process.BeginErrorReadLine();

            return true;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "No se pudo iniciar el proceso de descarga para {Codigo}", codigoExterno);
            EnCurso.TryRemove(licitacionId, out _);
            return false;
        }
    }

    private string? ResolverScriptPath()
    {
        var configPath = config["Extraccion:ScriptDescargaPath"];
        if (!string.IsNullOrWhiteSpace(configPath))
        {
            var full = Path.GetFullPath(configPath, AppContext.BaseDirectory);
            if (File.Exists(full)) return full;
            var cwdFull = Path.GetFullPath(configPath, Directory.GetCurrentDirectory());
            if (File.Exists(cwdFull)) return cwdFull;
            return null;
        }

        var publishPath = Path.Combine(AppContext.BaseDirectory, "tools", "descargar-documentos.js");
        if (File.Exists(publishPath)) return publishPath;

        var repoPath = Path.GetFullPath(Path.Combine("..", "tools", "scraper-mp-v2", "descargar-documentos.js"));
        if (File.Exists(repoPath)) return repoPath;

        return null;
    }

    /// <summary>
    /// Detecta si <c>xvfb-run</c> está disponible en el PATH. Si lo está, la descarga de
    /// documentos se envuelve con <c>xvfb-run --auto-servernum --</c> para correr Chromium en
    /// modo headed dentro de un framebuffer virtual, evitando reCAPTCHA en Cloud Run (mismo
    /// patrón que ScraperBackgroundService). En local (sin Xvfb instalado) retorna <c>false</c>
    /// y el script cae al modo headless.
    /// </summary>
    private static bool IsXvfbAvailable()
    {
        try
        {
            using var probe = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = "xvfb-run",
                    Arguments = "--help",
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    CreateNoWindow = true,
                }
            };
            probe.Start();
            probe.WaitForExit(3000);
            return probe.ExitCode == 0;
        }
        catch
        {
            return false;
        }
    }

    private static AdjuntoDocumentoDto MapToDto(AdjuntoDocumentosHandler.AdjuntoDocumentoFila f) => new()
    {
        Id = f.Id,
        Tipo = f.Tipo,
        NombreArchivo = f.NombreArchivo,
        TamanioBytes = f.TamanioBytes,
        MimeType = f.MimeType,
        Sha256Hash = f.Sha256Hash,
        FechaGrilla = f.FechaGrilla,
        Version = f.Version,
        EsActa = f.ActaDescargada,
        DescargaEstado = f.DescargaEstado,
        DescargadoAt = f.DescargaFinAt,
    };
}
