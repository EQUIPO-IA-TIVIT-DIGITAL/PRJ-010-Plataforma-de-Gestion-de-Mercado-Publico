using System.Security.Cryptography;
using Dapper;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using MPM.Modules.Licitaciones.Data;
using MPM.Shared.Services;

namespace MPM.Modules.Licitaciones.Services;

/// <summary>
/// Carga manual de pliegos (ADR-015, 038-carga-manual-pliegos).
/// Valida, sube a GCS/Local (IStorageService), calcula SHA-256 y persiste
/// con usp_Adjuntos_UpsertManual (metodo_extraccion='manual').
/// Reutiliza la misma tabla licitaciones_adjuntos que el scraper.
/// </summary>
public class AdjuntoManualUploadService(
    ILogger<AdjuntoManualUploadService> logger,
    AdjuntoDocumentosHandler handler,
    ExtraccionLogHandler extraccionLogHandler,
    IStorageService storageService,
    IConfiguration config)
{
    private static readonly HashSet<string> ExtPermitidas = new(StringComparer.OrdinalIgnoreCase)
    { ".pdf", ".doc", ".docx", ".xls", ".xlsx", ".zip", ".rar", ".txt" };

    private static readonly HashSet<string> MimePermitidos = new(StringComparer.OrdinalIgnoreCase)
    {
        "application/pdf",
        "application/msword",
        "application/vnd.openxmlformats-officedocument.wordprocessingml.document",
        "application/vnd.ms-excel",
        "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
        "application/zip",
        "application/x-zip-compressed",
        "application/vnd.rar",
        "text/plain",
        "application/octet-stream" // fallback, se valida por extensión/magic
    };

    private const long MaxBytesPorArchivo = 20L * 1024 * 1024; // 20MB
    private const int MaxArchivosPorRequest = 10;

    public record UploadManualResult(
        int TotalRecibidos,
        int Descargados,
        int Reutilizados,
        int Rechazados,
        List<string> Errores,
        string? ConjuntoHash);

    public async Task<UploadManualResult> UploadAsync(
        long licitacionId, string codigoExterno, List<IFormFile> files, string usuario, CancellationToken ct = default)
    {
        if (files.Count == 0)
            throw new ArgumentException("No se recibieron archivos");

        if (files.Count > MaxArchivosPorRequest)
            throw new ArgumentException($"Máximo {MaxArchivosPorRequest} archivos por request");

        var descargados = 0;
        var reutilizados = 0;
        var rechazados = 0;
        var errores = new List<string>();

        foreach (var file in files)
        {
            var nombreOriginal = file.FileName?.Trim() ?? $"archivo-{Guid.NewGuid()}";
            var ext = Path.GetExtension(nombreOriginal).ToLowerInvariant();

            // Validación extensión
            if (!ExtPermitidas.Contains(ext))
            {
                rechazados++;
                errores.Add($"{nombreOriginal}: extensión no permitida ({ext}) - DOC_008");
                continue;
            }

            if (file.Length == 0)
            {
                rechazados++;
                errores.Add($"{nombreOriginal}: archivo vacío");
                continue;
            }

            if (file.Length > MaxBytesPorArchivo)
            {
                rechazados++;
                errores.Add($"{nombreOriginal}: supera 20MB ({file.Length / 1024 / 1024}MB) - VAL_001");
                continue;
            }

            // Leer bytes y validar magic bytes para PDF
            byte[] bytes;
            try
            {
                await using var ms = new MemoryStream();
                await file.CopyToAsync(ms, ct);
                bytes = ms.ToArray();
            }
            catch (Exception ex)
            {
                rechazados++;
                errores.Add($"{nombreOriginal}: error leyendo archivo - {ex.Message}");
                continue;
            }

            if (ext == ".pdf" && !EsPdfValido(bytes))
            {
                rechazados++;
                errores.Add($"{nombreOriginal}: magic bytes no corresponden a PDF (DOC_009)");
                continue;
            }

            // MIME por extensión (más confiable que file.ContentType del browser)
            var mime = MimePorExtension(ext, file.ContentType);

            // Sanitizar y generar ruta storage
            var sanitizado = Sanitizar(nombreOriginal);
            var uuid = Guid.NewGuid().ToString("N")[..12];
            var storageFileName = $"{uuid}_{sanitizado}";
            var storagePath = $"licitaciones/{codigoExterno}/manual";
            var fullStoragePath = $"{storagePath}/{storageFileName}";

            // SHA-256
            var sha = Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant();

            // Subir a GCS/Local
            string rutaStorage;
            try
            {
                await using var uploadStream = new MemoryStream(bytes);
                rutaStorage = await storageService.UploadAsync(storagePath, storageFileName, uploadStream, mime, ct);
                // LocalStorageService retorna fullPath local; para consistencia, guardamos ruta relativa si es local
                // Si es GCS, ya es gs://...; si es local, es C:\...\uploads\...
                // Guardamos tal cual retorna el servicio (el handler lo resuelve en ambos casos)
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Error subiendo {Archivo} a storage", nombreOriginal);
                rechazados++;
                errores.Add($"{nombreOriginal}: error subiendo a storage - {ex.Message}");
                continue;
            }

            // Persistir en BD con metodo_extraccion='manual'
            try
            {
                var filas = await handler.ListarAsync(licitacionId, ct);
                // Necesitamos llamar al SP manual. Usamos Dapper directo via handler expuesta o raw.
                // El handler no expone UpsertManual aún, así que lo hacemos via Dapper aquí usando el factory.
                // Delegamos al handler con nuevo método UpsertManualAsync (ver abajo) — si no existe, fallback a SQL.
                var (pId, pVersion, pCreado, pError) = await UpsertManualAsync(
                    licitacionId, nombreOriginal, storageFileName, fullStoragePath, rutaStorage,
                    bytes.Length, mime, sha, ct);

                if (!string.IsNullOrWhiteSpace(pError) && !pError.StartsWith("SYS"))
                {
                    // SYS_ son errores reales; otros vacíos son ok (ver SP)
                    descargados++;
                    logger.LogInformation("Manual upload OK {Archivo} -> {Storage} v{Version} sha={Sha} lic={Lic}",
                        nombreOriginal, fullStoragePath, pVersion, sha[..12], codigoExterno);
                }
                else if (!string.IsNullOrWhiteSpace(pError))
                {
                    logger.LogError("Error BD upsert {Archivo}: {Error}", nombreOriginal, pError);
                    rechazados++;
                    errores.Add($"{nombreOriginal}: error BD - {pError}");
                    continue;
                }
                else
                {
                    if (pCreado) descargados++; else reutilizados++;
                }
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Error persistiendo {Archivo} en BD", nombreOriginal);
                rechazados++;
                errores.Add($"{nombreOriginal}: error BD - {ex.Message}");
            }
        }

        // Log de extracción manual para trazabilidad
        try
        {
            var estado = rechazados == files.Count ? "fallo" : "exito";
            var errorLog = errores.Count > 0 ? string.Join("; ", errores.Take(3)) : null;
            await extraccionLogHandler.RegistrarAsync(licitacionId, "manual", estado, descargados + reutilizados, false, false, errorLog, 0, ct);
        }
        catch (Exception ex) { logger.LogWarning(ex, "ManualUpload extraccionLog fallback lic {LicitacionId}", licitacionId); }

        // Calcular conjuntoHash actualizado
        string? conjuntoHash = null;
        try
        {
            var filasActuales = await handler.ListarAsync(licitacionId, ct);
            conjuntoHash = AdjuntoDocumentosHash.CalcularConjuntoHash(filasActuales);
        }
        catch (Exception ex) { logger.LogWarning(ex, "ManualUpload conjuntoHash fallback lic {LicitacionId}", licitacionId); }

        return new UploadManualResult(files.Count, descargados, reutilizados, rechazados, errores, conjuntoHash);
    }

    private async Task<(long pId, int pVersion, bool pCreado, string? pError)> UpsertManualAsync(
        long licitacionId, string nombreArchivo, string storageFileName, string fullStoragePath, string rutaStorage,
        long tamanio, string mime, string sha, CancellationToken ct)
    {
        // Usamos el factory del handler via reflection o creamos conexión directa
        // Más simple: el handler expone DbConnectionFactory, lo reutilizamos via el mismo que usa ListarAsync
        // Para no exponer, hacemos una extensión: llamamos a un nuevo método del handler si existe,
        // sino hacemos SQL directo con Dapper.
        // Aquí implementamos directo con Dapper usando la misma connection string del handler.
        // Truco: obtener factory via campo private _dbFactory (reflection)
        var field = typeof(AdjuntoDocumentosHandler).GetField("_dbFactory", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        var factory = (MPM.Core.Data.DbConnectionFactory)field!.GetValue(handler)!;
        await using var conn = factory.Create();
        var p = new Dapper.DynamicParameters();
        p.Add("p_licitacion_id", licitacionId);
        p.Add("p_tipo", "anexo");
        p.Add("p_nombre_archivo", nombreArchivo);
        p.Add("p_ruta_storage", fullStoragePath);
        p.Add("p_nombre_elemento", storageFileName);
        p.Add("p_ruta_local", rutaStorage); // en local es full path, en GCS es gs://...
        p.Add("p_tamanio_bytes", tamanio);
        p.Add("p_mime_type", mime);
        p.Add("p_es_acta", false);
        p.Add("p_sha256_hash", sha);
        p.Add("p_fecha_grilla", DateTime.UtcNow.ToString("dd-MM-yyyy"));
        p.Add("p_metodo_extraccion", "manual");
        p.Add("p_id", dbType: System.Data.DbType.Int64, direction: System.Data.ParameterDirection.InputOutput);
        p.Add("p_version", dbType: System.Data.DbType.Int32, direction: System.Data.ParameterDirection.InputOutput);
        p.Add("p_creado", dbType: System.Data.DbType.Boolean, direction: System.Data.ParameterDirection.InputOutput);
        p.Add("p_error_msg", dbType: System.Data.DbType.String, size: 1000, direction: System.Data.ParameterDirection.InputOutput);

        await conn.ExecuteAsync(
            "CALL usp_Adjuntos_UpsertManual(@p_licitacion_id, @p_tipo, @p_nombre_archivo, @p_ruta_storage, @p_nombre_elemento, @p_ruta_local, @p_tamanio_bytes, @p_mime_type, @p_es_acta, @p_sha256_hash, @p_fecha_grilla, @p_metodo_extraccion, @p_id, @p_version, @p_creado, @p_error_msg)",
            p, commandType: System.Data.CommandType.Text);

        var pId = p.Get<long?>("p_id") ?? 0;
        var pVersion = p.Get<int?>("p_version") ?? 0;
        var pCreado = p.Get<bool?>("p_creado") ?? false;
        var pError = p.Get<string?>("p_error_msg");
        return (pId, pVersion, pCreado, pError);
    }

    private static bool EsPdfValido(byte[] bytes)
    {
        if (bytes.Length < 4) return false;
        // %PDF
        return bytes[0] == 0x25 && bytes[1] == 0x50 && bytes[2] == 0x44 && bytes[3] == 0x46;
    }

    private static string MimePorExtension(string ext, string? contentType)
    {
        return ext.ToLowerInvariant() switch
        {
            ".pdf" => "application/pdf",
            ".doc" => "application/msword",
            ".docx" => "application/vnd.openxmlformats-officedocument.wordprocessingml.document",
            ".xls" => "application/vnd.ms-excel",
            ".xlsx" => "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
            ".zip" => "application/zip",
            ".rar" => "application/vnd.rar",
            ".txt" => "text/plain",
            _ => string.IsNullOrWhiteSpace(contentType) ? "application/octet-stream" : contentType
        };
    }

    private static string Sanitizar(string nombre)
    {
        var invalid = Path.GetInvalidFileNameChars();
        var sanitized = string.Join("_", nombre.Split(invalid, StringSplitOptions.RemoveEmptyEntries)).Trim();
        sanitized = sanitized.Replace(" ", "_");
        if (string.IsNullOrWhiteSpace(sanitized)) sanitized = $"archivo_{Guid.NewGuid():N}";
        // Limitar longitud
        if (sanitized.Length > 120) sanitized = sanitized[..120];
        return sanitized;
    }
}
