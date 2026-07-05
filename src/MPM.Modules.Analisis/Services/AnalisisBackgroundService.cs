using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using MPM.Modules.Analisis.Data;
using MPM.Modules.Analisis.Models;
using MPM.Shared.Services;

namespace MPM.Modules.Analisis.Services;

public interface IAnalisisBackgroundService
{
    void EnqueueAnalisis(long workspaceId, long documentoId, string documentoNombre, string rutaStorage, string geminiApiKey);
}

public class AnalisisBackgroundService : IAnalisisBackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<AnalisisBackgroundService> _logger;
    private readonly HashSet<long> _activeAnalisis = new();

    public AnalisisBackgroundService(IServiceScopeFactory scopeFactory, ILogger<AnalisisBackgroundService> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    public void EnqueueAnalisis(long workspaceId, long documentoId, string documentoNombre, string rutaStorage, string geminiApiKey)
    {
        lock (_activeAnalisis)
        {
            if (_activeAnalisis.Contains(workspaceId))
            {
                _logger.LogWarning("Análisis ya en progreso para workspace {WorkspaceId}", workspaceId);
                return;
            }
            _activeAnalisis.Add(workspaceId);
        }

        _ = Task.Run(async () =>
        {
            try
            {
                await ProcessAnalisisAsync(workspaceId, documentoId, documentoNombre, rutaStorage, geminiApiKey);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error inesperado en análisis background para workspace {WorkspaceId}", workspaceId);
                await SetErrorState(workspaceId);
            }
            finally
            {
                lock (_activeAnalisis)
                {
                    _activeAnalisis.Remove(workspaceId);
                }
            }
        });
    }

    private async Task ProcessAnalisisAsync(long workspaceId, long documentoId, string documentoNombre, string rutaStorage, string geminiApiKey)
    {
        using var scope = _scopeFactory.CreateScope();
        var handler = scope.ServiceProvider.GetRequiredService<AnalisisHandler>();
        var geminiService = scope.ServiceProvider.GetRequiredService<GeminiService>();
        var storageService = scope.ServiceProvider.GetRequiredService<IStorageService>();

        _logger.LogInformation("Iniciando análisis background para workspace {WorkspaceId}, documento {DocumentoId}", workspaceId, documentoId);

        try
        {
            byte[] pdfBytes;
            if (rutaStorage.StartsWith("gs://"))
            {
                var stream = await storageService.DownloadAsync(rutaStorage, CancellationToken.None);
                if (stream == null) throw new InvalidOperationException("No se pudo leer el PDF desde el storage");
                using var ms = new MemoryStream();
                await stream.CopyToAsync(ms, CancellationToken.None);
                pdfBytes = ms.ToArray();
            }
            else
            {
                pdfBytes = await File.ReadAllBytesAsync(rutaStorage, CancellationToken.None);
            }

            _logger.LogInformation("PDF {File} descargado ({Size} bytes), enviando a Gemini", documentoNombre, pdfBytes.Length);

            var geminiResponse = await geminiService.AnalyzePdfAsync(pdfBytes, documentoNombre, geminiApiKey, CancellationToken.None);

            if (string.IsNullOrEmpty(geminiResponse.Text))
            {
                _logger.LogWarning("Respuesta vacía de Gemini para workspace {WorkspaceId}", workspaceId);
                await SetErrorState(workspaceId);
                return;
            }

            // PostgreSQL JSONB rejects null bytes — strip them before saving
            var jsonText = geminiResponse.Text.Replace("\0", "");

            // Validate that it's parseable JSON before sending to DB
            try { System.Text.Json.JsonDocument.Parse(jsonText); }
            catch (Exception ex)
            {
                _logger.LogError("Texto de Gemini no es JSON válido para workspace {WorkspaceId}: {Msg}. Primeros 200: {Preview}",
                    workspaceId, ex.Message, jsonText.Length > 200 ? jsonText[..200] : jsonText);
                await SetErrorState(workspaceId);
                return;
            }

            // Validación documental determinística: contrastar lo que declara el acta
            // contra los archivos realmente subidos al workspace (agrega inconsistencias
            // que Gemini no haya detectado; nunca elimina las existentes)
            try
            {
                var docsWorkspace = await handler.ListarDocumentosAsync(workspaceId, CancellationToken.None);
                var nombresEnviados = docsWorkspace.Select(d => d.NombreArchivo).ToList();
                jsonText = ValidacionDocumentalService.AplicarValidacion(jsonText, nombresEnviados);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Validación documental falló para workspace {WorkspaceId}; se guarda el análisis sin post-proceso", workspaceId);
            }

            var (resultadoId, insError) = await handler.CrearResultadoAsync(
                workspaceId, documentoId, jsonText, GeminiService.ModelName,
                geminiResponse.Usage.PromptTokenCount, geminiResponse.Usage.CandidatesTokenCount, CancellationToken.None);

            if (insError != null)
            {
                _logger.LogError("Error guardando resultado: {Error}", insError);
                await SetErrorState(workspaceId);
                return;
            }

            _logger.LogInformation("Análisis completado para workspace {WorkspaceId}, resultado {ResultadoId}", workspaceId, resultadoId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error en análisis de workspace {WorkspaceId}", workspaceId);
            await SetErrorState(workspaceId);
        }
    }

    private async Task SetErrorState(long workspaceId)
    {
        try
        {
            using var scope = _scopeFactory.CreateScope();
            var handler = scope.ServiceProvider.GetRequiredService<AnalisisHandler>();
            await handler.ActualizarEstadoAsync(workspaceId, "error", CancellationToken.None);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error actualizando estado a 'error' para workspace {WorkspaceId}", workspaceId);
        }
    }
}
