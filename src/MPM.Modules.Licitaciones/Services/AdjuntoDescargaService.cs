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
    private static readonly ConcurrentDictionary<long, byte> EnCurso = new();

    /// <summary>Excepción tipada: ya hay una extracción en curso para esta licitación.</summary>
    public class DescargaEnCursoException : Exception
    {
        public DescargaEnCursoException(string message) : base(message) { }
    }

    public async Task<EstadoDocumentosDto> ObtenerEstadoAsync(long licitacionId, CancellationToken ct = default)
    {
        var filas = await handler.ListarAsync(licitacionId, ct);

        var estado = filas.Count == 0
            ? "pendiente"
            : filas.Any(f => f.DescargaEstado == "descargando") ? "descargando"
            : filas.FirstOrDefault(f => f.DescargaEstado == "error") is { } err ? "error"
            : "completado";

        var error = filas.FirstOrDefault(f => f.DescargaEstado == "error")?.DescargaError;

        // Sin filas de adjuntos: el estado se deriva del último intento de extracción (si falló,
        // el usuario debe ver el motivo y poder reintentar — antes quedaba invisible como "pendiente").
        if (filas.Count == 0 && estado == "pendiente")
        {
            var ultima = await handler.ObtenerUltimaExtraccionAsync(licitacionId, ct);
            if (ultima is { Estado: "fallo" } && !string.IsNullOrWhiteSpace(ultima.Error))
            {
                estado = "error";
                error = ultima.Error;
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
        if (!EnCurso.TryAdd(licitacionId, 0))
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

            var arranco = DispararScraper(scriptPath, licitacionId, codigoExterno);
            if (!arranco)
                return await FallarAsync(licitacionId, "El proceso de descarga no arrancó (script/node no disponible)", ct);

            await extraccionLogHandler.RegistrarAsync(
                licitacionId, "navegador", "exito", 0, false, false, null, 0, ct);

            return new DescargarDocumentosResultDto { EstadoConjunto = "descargando", Accion = "descargando" };
        }
        finally
        {
            EnCurso.TryRemove(licitacionId, out _);
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

        // Mismas credenciales de DB que el scraper diario + credenciales MP para login.
        startInfo.EnvironmentVariables["DB_HOST"] = config["DB_HOST"] ?? "db";
        startInfo.EnvironmentVariables["DB_PORT"] = config["DB_PORT"] ?? "5432";
        startInfo.EnvironmentVariables["DB_NAME"] = config["DB_NAME"] ?? "";
        startInfo.EnvironmentVariables["DB_USER"] = config["DB_USER"] ?? "";
        startInfo.EnvironmentVariables["DB_PASSWORD"] = config["DB_PASSWORD"] ?? "";
        startInfo.EnvironmentVariables["DB_SSL"] = config["DB_SSL"] ?? "";
        startInfo.EnvironmentVariables["MP_RUT"] = config["MP_RUT"] ?? "";
        startInfo.EnvironmentVariables["MP_PASSWORD"] = config["MP_PASSWORD"] ?? "";
        startInfo.EnvironmentVariables["MP_HEADLESS"] = config["MP_HEADLESS"] ?? "true";
        // Default: raíz de uploads del proceso (cwd/uploads) — el storage local del backend.
        // OJO: una clave vacía en appsettings NO debe anular el default ("" ?? default = "").
        var adjuntosDir = config["Extraccion:AdjuntosDir"];
        startInfo.EnvironmentVariables["ADJUNTOS_DIR"] = string.IsNullOrWhiteSpace(adjuntosDir)
            ? Path.Combine(Directory.GetCurrentDirectory(), "uploads")
            : adjuntosDir;

        logger.LogInformation(
            "Disparando descarga de documentos para licitación {Codigo} (id={LicitacionId}) con script {Script}",
            codigoExterno, licitacionId, scriptPath);

        try
        {
            var process = new Process { StartInfo = startInfo };
            process.Start();

            // Espera corta para detectar fallas inmediatas (script ausente, módulo roto, DB
            // inalcanzable al boot). Una corrida legítima tarda minutos: si sigue viva tras 5s
            // se deja correr fire-and-forget (persiste directo en Postgres).
            if (process.WaitForExit(5000) && process.ExitCode != 0)
            {
                var stderr = process.StandardError.ReadToEnd();
                logger.LogError(
                    "El proceso de descarga para {Codigo} salió inmediatamente con código {ExitCode}: {Stderr}",
                    codigoExterno, process.ExitCode, stderr);
                return false;
            }

            return true;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "No se pudo iniciar el proceso de descarga para {Codigo}", codigoExterno);
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
