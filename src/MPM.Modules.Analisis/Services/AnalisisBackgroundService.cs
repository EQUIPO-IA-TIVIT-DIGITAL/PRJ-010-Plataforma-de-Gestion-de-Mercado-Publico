using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using MPM.Modules.Analisis.Data;
using MPM.Modules.Analisis.Models;
using MPM.Shared.Services;

namespace MPM.Modules.Analisis.Services;

public interface IAnalisisBackgroundService
{
    /// <summary>
    /// 029-fix-hallazgos-code-review-competidores-alertas (FR-011/QA BUG-005): recibe todos los
    /// documentos a analizar (antes solo el primero) -- <paramref name="documentoRepresentativoId"/>
    /// es el que se usa como FK del resultado guardado (el más reciente del conjunto), pero el
    /// contenido enviado a Gemini incluye TODOS los <paramref name="documentos"/>.
    /// </summary>
    void EnqueueAnalisis(long workspaceId, long documentoRepresentativoId,
        List<(long Id, string Nombre, string RutaStorage)> documentos);
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

    public void EnqueueAnalisis(long workspaceId, long documentoRepresentativoId,
        List<(long Id, string Nombre, string RutaStorage)> documentos)
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
                await ProcessAnalisisAsync(workspaceId, documentoRepresentativoId, documentos);
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

    private async Task ProcessAnalisisAsync(long workspaceId, long documentoRepresentativoId,
        List<(long Id, string Nombre, string RutaStorage)> documentos)
    {
        using var scope = _scopeFactory.CreateScope();
        var handler = scope.ServiceProvider.GetRequiredService<AnalisisHandler>();
        var geminiService = scope.ServiceProvider.GetRequiredService<GeminiService>();
        var storageService = scope.ServiceProvider.GetRequiredService<IStorageService>();

        _logger.LogInformation("Iniciando análisis background para workspace {WorkspaceId}, {Count} documento(s)", workspaceId, documentos.Count);

        try
        {
            // Si ya está en GCS, se referencia directo (gcsUri) sin necesitar los bytes en el
            // body de la request a Gemini (020-migracion-gemini-adc) — igual se descargan acá
            // porque el resto del pipeline (nombre, tamaño) los sigue usando, pero GeminiService
            // ignora pdfBytes cuando gcsUri no es null.
            var documentosParaGemini = new List<(byte[] Bytes, string FileName, string? GcsUri)>();
            foreach (var (_, nombre, rutaStorage) in documentos)
            {
                byte[] pdfBytes;
                string? gcsUri = null;
                if (rutaStorage.StartsWith("gs://"))
                {
                    gcsUri = rutaStorage;
                    var stream = await storageService.DownloadAsync(rutaStorage, CancellationToken.None);
                    if (stream == null) throw new InvalidOperationException($"No se pudo leer el PDF '{nombre}' desde el storage");
                    using var ms = new MemoryStream();
                    await stream.CopyToAsync(ms, CancellationToken.None);
                    pdfBytes = ms.ToArray();
                }
                else
                {
                    pdfBytes = await File.ReadAllBytesAsync(rutaStorage, CancellationToken.None);
                }
                documentosParaGemini.Add((pdfBytes, nombre, gcsUri));
            }

            _logger.LogInformation("{Count} documento(s) descargado(s) ({TotalBytes} bytes), enviando a Gemini",
                documentosParaGemini.Count, documentosParaGemini.Sum(d => d.Bytes.Length));

            var geminiResponse = await geminiService.AnalyzeDocumentosAsync(documentosParaGemini, CancellationToken.None);

            if (string.IsNullOrEmpty(geminiResponse.Text))
            {
                _logger.LogWarning("Respuesta vacía de Gemini para workspace {WorkspaceId}", workspaceId);
                await SetErrorState(workspaceId);
                return;
            }

            // PostgreSQL JSONB rejects null bytes — strip them before saving
            var jsonText = geminiResponse.Text.Replace("\0", "");

            // Validate that it's parseable JSON before sending to DB
            System.Text.Json.JsonDocument parsedDoc;
            try { parsedDoc = System.Text.Json.JsonDocument.Parse(jsonText); }
            catch (Exception ex)
            {
                _logger.LogError("Texto de Gemini no es JSON válido para workspace {WorkspaceId}: {Msg}. Primeros 200: {Preview}",
                    workspaceId, ex.Message, jsonText.Length > 200 ? jsonText[..200] : jsonText);
                await SetErrorState(workspaceId);
                return;
            }

            // 029-fix-hallazgos-code-review-competidores-alertas (FR-011): el prompt exige un
            // único objeto JSON incluso con múltiples documentos, pero no hay garantía de que el
            // modelo lo respete siempre -- si responde con un array (ej. interpretó cada
            // documento como una licitación separada), el resto del pipeline (dashboard, chat)
            // espera un objeto en la raíz y lo descartaría silenciosamente. Se toma el primer
            // elemento en vez de fallar todo el análisis, y se deja constancia en el log.
            using (parsedDoc)
            {
                if (parsedDoc.RootElement.ValueKind == System.Text.Json.JsonValueKind.Array)
                {
                    _logger.LogWarning(
                        "Gemini devolvió un array de {Count} elemento(s) en vez de un único objeto para workspace {WorkspaceId} ({DocCount} documentos enviados) -- se usa el primer elemento.",
                        parsedDoc.RootElement.GetArrayLength(), workspaceId, documentosParaGemini.Count);
                    if (parsedDoc.RootElement.GetArrayLength() == 0)
                    {
                        _logger.LogError("Array de respuesta vacío para workspace {WorkspaceId}", workspaceId);
                        await SetErrorState(workspaceId);
                        return;
                    }
                    jsonText = parsedDoc.RootElement[0].GetRawText();
                }
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

            // 029-fix-hallazgos-code-review-competidores-alertas (FR-017/US13, QA BUG-007):
            // normaliza menciones de moneda en prosa (ej. "DÓLAR AMERICANO") a la misma sigla que
            // usa el formateador del frontend (ej. "US$"), para que no queden contradictorias.
            try
            {
                jsonText = MonedaNormalizerService.Normalizar(jsonText);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Normalización de moneda en texto libre falló para workspace {WorkspaceId}; se guarda el análisis sin este post-proceso", workspaceId);
            }

            var (resultadoId, insError) = await handler.CrearResultadoAsync(
                workspaceId, documentoRepresentativoId, jsonText, GeminiService.ModelName,
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
