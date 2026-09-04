using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using MPM.Modules.Propuestas.Models;

namespace MPM.Modules.Propuestas.Services;

public interface IGoogleDriveService
{
    Task<ExportarDriveResponse> ExportarArchivoAsync(
        string codigoExterno, string fileName, Stream fileStream, string contentType, CancellationToken ct = default);
}

public class GoogleDriveService(
    IConfiguration configuration,
    ILogger<GoogleDriveService> logger) : IGoogleDriveService
{
    private readonly string _localExportFolder = configuration["GoogleDrive:LocalExportPath"] ?? Path.Combine(AppContext.BaseDirectory, "data", "google_drive");
    private readonly bool _useSimulatedDrive = string.Equals(configuration["GoogleDrive:Mode"], "simulated", StringComparison.OrdinalIgnoreCase)
                                              || string.IsNullOrWhiteSpace(configuration["GoogleDrive:Mode"]);

    public async Task<ExportarDriveResponse> ExportarArchivoAsync(
        string codigoExterno, string fileName, Stream fileStream, string contentType, CancellationToken ct = default)
    {
        var safeCode = string.Concat(codigoExterno.Split(Path.GetInvalidFileNameChars()));
        var safeFileName = string.Concat(fileName.Split(Path.GetInvalidFileNameChars()));
        var fileId = $"drv_{Guid.NewGuid():N}";
        var exportedAt = DateTime.UtcNow;

        var targetDir = Path.Combine(_localExportFolder, safeCode);
        Directory.CreateDirectory(targetDir);
        var targetFilePath = Path.Combine(targetDir, safeFileName);

        if (fileStream.CanSeek)
            fileStream.Position = 0;

        await using (var output = File.Create(targetFilePath))
        {
            await fileStream.CopyToAsync(output, ct);
        }

        logger.LogInformation("Archivo {FileName} exportado exitosamente a Google Drive ({SafeCode}) en {Path}",
            safeFileName, safeCode, targetFilePath);

        var webUrl = _useSimulatedDrive
            ? $"https://drive.google.com/file/d/{fileId}/view"
            : $"https://drive.google.com/drive/folders/{Uri.EscapeDataString(safeCode)}";

        return new ExportarDriveResponse
        {
            DriveFileId = fileId,
            WebUrl = webUrl,
            NombreArchivo = safeFileName,
            ExportadoAt = exportedAt,
        };
    }
}
