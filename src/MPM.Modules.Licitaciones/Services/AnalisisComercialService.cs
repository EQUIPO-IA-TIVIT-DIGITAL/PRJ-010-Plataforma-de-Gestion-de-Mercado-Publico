using System.Collections.Concurrent;
using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using MPM.Core.SystemConfig;
using MPM.Modules.Licitaciones.Data;
using MPM.Modules.Licitaciones.Models;
using MPM.Shared.Services;

namespace MPM.Modules.Licitaciones.Services;

/// <summary>
/// Zona IA on-demand (036-flujo-comercial-ofertas, Fase 1.3): analiza los documentos de una
/// licitación descargados (V141) con el proveedor IA activo de MPM (LlmClientResolver) y
/// produce el análisis comercial: datos clave, requisitos, criterios, riesgos, match TIVIT
/// preliminar, estimación orientativa y recomendación GO/NO GO (la decisión final es humana).
///
/// Cache por conjuntoHash (V142): si la misma versión de documentos ya fue analizada, el
/// resultado se devuelve sin re-pagar IA. El request nunca espera al LLM: responde
/// "analizando" y el frontend hace polling (patrón de los workspaces de análisis).
/// </summary>
public class AnalisisComercialService(
    ILogger<AnalisisComercialService> logger,
    LlmClientResolver resolver,
    AdjuntoDocumentosHandler adjuntosHandler,
    AnalisisComercialHandler analisisHandler,
    IStorageService storageService,
    IServiceScopeFactory scopeFactory)
{
    private static readonly ConcurrentDictionary<long, byte> EnCurso = new();

    public class SinDocumentosException : Exception
    {
        public SinDocumentosException(string message) : base(message) { }
    }

    public class AnalisisEnCursoException : Exception
    {
        public AnalisisEnCursoException(string message) : base(message) { }
    }

    public async Task<AnalisisComercialEstadoDto> ObtenerEstadoAsync(long licitacionId, CancellationToken ct = default)
    {
        var filas = await adjuntosHandler.ListarAsync(licitacionId, ct);
        var hashActual = AdjuntoDocumentosHash.CalcularConjuntoHash(filas);

        var ultimo = await analisisHandler.ObtenerUltimoAsync(licitacionId, ct);
        if (ultimo == null)
        {
            return new AnalisisComercialEstadoDto
            {
                Estado = "pendiente",
                ConjuntoHash = hashActual,
                Desactualizado = hashActual != null && ultimo == null,
            };
        }

        var resultado = ParseResultado(ultimo.ResultadoJson);

        return new AnalisisComercialEstadoDto
        {
            Estado = ultimo.Estado,
            Error = ultimo.Error,
            ConjuntoHash = ultimo.ConjuntoHash,
            // Si los documentos cambiaron respecto al análisis guardado → desactualizado.
            Desactualizado = hashActual != null && hashActual != ultimo.ConjuntoHash,
            ResumenEjecutivo = ultimo.ResumenEjecutivo,
            GoNoGo = ultimo.GoNoGo,
            ScoreConfianza = ultimo.ScoreConfianza,
            ModeloUsado = ultimo.ModeloUsado,
            TokensEntrada = ultimo.TokensEntrada,
            TokensSalida = ultimo.TokensSalida,
            CreadoPor = ultimo.CreadoPor,
            CreatedAt = ultimo.CreatedAt,
            UpdatedAt = ultimo.UpdatedAt,
            Resultado = resultado,
        };
    }

    /// <summary>
    /// Inicia el análisis comercial del conjunto actual de documentos. Cache hit si la misma
    /// versión ya fue analizada (no re-paga IA). Fire-and-forget para el LLM (patrón existente).
    /// </summary>
    public async Task<IniciarAnalisisComercialResultDto> IniciarAnalisisAsync(
        long licitacionId, string codigoExterno, string usuario, CancellationToken ct = default)
    {
        var filas = await adjuntosHandler.ListarAsync(licitacionId, ct);
        if (filas.Count == 0)
            throw new SinDocumentosException("Esta licitación aún no tiene documentos descargados. Usa 'Descargar documentos' primero.");

        var conjuntoHash = AdjuntoDocumentosHash.CalcularConjuntoHash(filas);
        if (string.IsNullOrWhiteSpace(conjuntoHash))
            throw new SinDocumentosException("Los documentos no tienen hash calculado. Descarga los documentos nuevamente para auditar sus hashes.");

        // Cache: misma versión ya analizada → devolver sin re-pagar IA.
        var ultimo = await analisisHandler.ObtenerUltimoAsync(licitacionId, ct);
        if (ultimo is { Estado: "completado" } && ultimo.ConjuntoHash == conjuntoHash)
        {
            logger.LogInformation("Análisis comercial cacheado para licitación {Codigo} (conjunto {Hash}) — sin llamar al LLM",
                codigoExterno, HashCorto(conjuntoHash));
            return new IniciarAnalisisComercialResultDto { Estado = "completado", CacheHit = true, ConjuntoHash = conjuntoHash };
        }

        // Guard de concurrencia in-process (doble clic simultáneo).
        if (!EnCurso.TryAdd(licitacionId, 0))
            throw new AnalisisEnCursoException("Ya hay un análisis en curso para esta licitación");

        try
        {
            // Idempotencia con estado persistido: 'analizando' reciente → no re-disparar.
            if (ultimo is { Estado: "analizando" } && DateTime.UtcNow - (ultimo.UpdatedAt ?? DateTime.UtcNow).ToUniversalTime() < TimeSpan.FromMinutes(10))
            {
                return new IniciarAnalisisComercialResultDto { Estado = "analizando", CacheHit = false, ConjuntoHash = conjuntoHash };
            }

            var (id, _, err) = await analisisHandler.IniciarAsync(licitacionId, conjuntoHash, usuario, ct);
            if (err != null || id == 0)
            {
                logger.LogError("No se pudo iniciar análisis comercial para licitación {LicitacionId}: {Error}", licitacionId, err);
                throw new InvalidOperationException("No se pudo iniciar el análisis (error interno)");
            }

            // Procesamiento asíncrono: el LLM puede tardar minutos (todos los PDFs en una llamada).
            // IMPORTANTE: el servicio es SCOPED — su IServiceProvider/LlmClientResolver se dispondría
            // al terminar el request. Se resuelve una instancia fresca dentro de un scope propio
            // (mismo patrón que AnalisisBackgroundService), viva durante toda la corrida.
            _ = Task.Run(async () =>
            {
                try
                {
                    await using var scope = scopeFactory.CreateAsyncScope();
                    var servicio = scope.ServiceProvider.GetRequiredService<AnalisisComercialService>();
                    await servicio.ProcesarAsync(id, filas, conjuntoHash, CancellationToken.None);
                }
                catch (Exception ex)
                {
                    logger.LogError(ex, "No se pudo ejecutar el análisis comercial en scope propio (id={AnalisisId})", id);
                    try
                    {
                        await analisisHandler.CompletarAsync(id, "error", null, null, null, null, null, null, null, ex.Message, CancellationToken.None);
                    }
                    catch (Exception ex2)
                    {
                        logger.LogError(ex2, "Tampoco se pudo marcar el error del análisis {AnalisisId}", id);
                    }
                }
            });

            return new IniciarAnalisisComercialResultDto { Estado = "analizando", CacheHit = false, ConjuntoHash = conjuntoHash };
        }
        finally
        {
            EnCurso.TryRemove(licitacionId, out _);
        }
    }

    /// <summary>Procesa el análisis: descarga bytes → LLM → saneado → persistencia. Publico para tests.</summary>
    public async Task ProcesarAsync(long analisisId, List<AdjuntoDocumentosHandler.AdjuntoDocumentoFila> filas, string conjuntoHash, CancellationToken ct = default)
    {
        try
        {
            // Se analizan documentos PDF, Word (.docx/.doc) y texto plano
            var analizables = filas.Where(EsDocumentoAnalizable).ToList();
            if (analizables.Count == 0)
            {
                await CompletarErrorAsync(analisisId, "Ninguno de los documentos descargados es un archivo analizable por la IA (PDF/Word/Texto)", ct);
                return;
            }

            var parts = new List<LlmPart>();
            var documentosCount = 0;

            foreach (var fila in analizables)
            {
                var bytes = await LeerBytesAsync(fila, ct);
                if (bytes == null)
                {
                    await CompletarErrorAsync(analisisId, $"No se pudo leer el documento {fila.NombreArchivo}", ct);
                    return;
                }

                var ext = Path.GetExtension(fila.NombreArchivo).ToLowerInvariant();
                if (ext == ".docx" || ext == ".doc" || ext == ".txt")
                {
                    var text = DocumentContentExtractor.ExtractText(bytes, fila.NombreArchivo);
                    if (!string.IsNullOrWhiteSpace(text))
                    {
                        parts.Add(new LlmTextPart(DocumentContentExtractor.FormatForPrompt(fila.NombreArchivo, text)));
                        documentosCount++;
                    }
                }
                else
                {
                    var gcsUri = fila.RutaStorage.StartsWith("gs://", StringComparison.OrdinalIgnoreCase) ? fila.RutaStorage : null;
                    parts.Add(new LlmPdfPart(bytes, fila.NombreArchivo, gcsUri));
                    documentosCount++;
                }
            }

            if (documentosCount == 0)
            {
                await CompletarErrorAsync(analisisId, "No se pudo extraer contenido útil de los documentos para el análisis", ct);
                return;
            }

            logger.LogInformation("Analizando {Count} documento(s) de la licitación (conjunto {Hash}) con el proveedor IA activo",
                documentosCount, HashCorto(conjuntoHash));

            parts.Add(new LlmTextPart(PromptAnalisisComercial(documentosCount)));

            var request = new LlmRequest(
                Messages: [new LlmMessage("user", parts)],
                Temperature: 0.2,
                MaxOutputTokens: VertexGeminiClient.DefaultMaxOutputTokens,
                JsonResponse: true);

            // Timeout explícito: el SDK de Google usa su propio transporte (el timeout de 5 min
            // del HttpClient del DI NO aplica a Gemini) — sin esto una llamada sin credenciales o
            // con cuota agotada puede quedar colgada para siempre en 'analizando'.
            using var llamadaCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
            llamadaCts.CancelAfter(TimeSpan.FromMinutes(10));

            var client = await resolver.GetClientAsync(llamadaCts.Token);
            var result = await client.GenerarContenidoAsync(request, llamadaCts.Token);

            var (json, resumen, goNoGo, score) = SanearYExtraer(result.Text);
            await analisisHandler.CompletarAsync(
                analisisId, "completado", json, resumen, goNoGo, score,
                client.ModelName,
                (int)(result.Usage.PromptTokenCount ?? 0), (int)(result.Usage.CandidatesTokenCount ?? 0),
                null, ct);
        }
        catch (OperationCanceledException) when (!ct.IsCancellationRequested)
        {
            logger.LogError("Timeout del análisis comercial (id={AnalisisId}): el proveedor IA no respondió en 10 minutos", analisisId);
            await CompletarErrorAsync(analisisId, "La consulta al proveedor de IA superó los 10 minutos", CancellationToken.None);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Falló el análisis comercial (id={AnalisisId})", analisisId);
            await CompletarErrorAsync(analisisId, ex.Message, CancellationToken.None);
        }
    }

    /// <summary>¿Es un PDF analizable por el LLM?</summary>
    internal static bool EsDocumentoPdf(AdjuntoDocumentosHandler.AdjuntoDocumentoFila fila)
        => fila.MimeType?.Equals("application/pdf", StringComparison.OrdinalIgnoreCase) == true
           || fila.NombreArchivo?.EndsWith(".pdf", StringComparison.OrdinalIgnoreCase) == true;

    /// <summary>¿Es un documento analizable por el LLM? (PDF, Word DOCX/DOC, Texto).</summary>
    internal static bool EsDocumentoAnalizable(AdjuntoDocumentosHandler.AdjuntoDocumentoFila fila)
    {
        var nombre = fila.NombreArchivo?.ToLowerInvariant() ?? "";
        var mime = fila.MimeType?.ToLowerInvariant() ?? "";
        return nombre.EndsWith(".pdf") || nombre.EndsWith(".docx") || nombre.EndsWith(".doc") || nombre.EndsWith(".txt")
               || mime == "application/pdf"
               || mime == "application/vnd.openxmlformats-officedocument.wordprocessingml.document"
               || mime == "application/msword"
               || mime == "text/plain";
    }

    private async Task CompletarErrorAsync(long analisisId, string error, CancellationToken ct)
        => await analisisHandler.CompletarAsync(analisisId, "error", null, null, null, null, null, null, null, error, ct);

    private async Task<byte[]?> LeerBytesAsync(AdjuntoDocumentosHandler.AdjuntoDocumentoFila fila, CancellationToken ct)
    {
        if (fila.RutaStorage.StartsWith("gs://", StringComparison.OrdinalIgnoreCase))
        {
            var stream = await storageService.DownloadAsync(fila.RutaStorage, ct);
            if (stream == null) return null;
            await using var ms = new MemoryStream();
            await stream.CopyToAsync(ms, ct);
            return ms.ToArray();
        }

        if (!string.IsNullOrWhiteSpace(fila.RutaLocal) && File.Exists(fila.RutaLocal))
            return await File.ReadAllBytesAsync(fila.RutaLocal, ct);

        var storageStream = await storageService.DownloadAsync(fila.RutaStorage, ct);
        if (storageStream == null) return null;
        await using var ms2 = new MemoryStream();
        await storageStream.CopyToAsync(ms2, ct);
        return ms2.ToArray();
    }

    /// <summary>Sanea la respuesta del LLM (strip \0, valida JSON, extrae campos top-level).</summary>
    internal static (string Json, string? Resumen, string? GoNoGo, decimal? Score) SanearYExtraer(string? texto)
    {
        if (string.IsNullOrWhiteSpace(texto))
            throw new InvalidOperationException("El proveedor de IA no devolvió contenido");

        var limpio = texto.Replace("\0", "").Trim();
        using var doc = JsonDocument.Parse(limpio);
        var root = doc.RootElement;

        // El modelo puede responder con un array cuando se le pasa un solo documento (patrón
        // conocido): tomar el primer elemento.
        if (root.ValueKind == JsonValueKind.Array && root.GetArrayLength() > 0)
            root = root[0];

        string? resumen = null;
        string? goNoGo = null;
        decimal? score = null;

        foreach (var prop in new[] { "resumen_ejecutivo", "resumenEjecutivo", "resumen" })
            if (root.TryGetProperty(prop, out var v) && v.ValueKind == JsonValueKind.String) { resumen = v.GetString(); break; }

        foreach (var prop in new[] { "go_no_go", "goNoGo", "recomendacion", "recomendacion_go_no_go" })
            if (root.TryGetProperty(prop, out var v) && v.ValueKind == JsonValueKind.String) { goNoGo = v.GetString(); break; }

        foreach (var prop in new[] { "score_confianza", "scoreConfianza", "score" })
            if (root.TryGetProperty(prop, out var v) &&
                (v.ValueKind == JsonValueKind.Number || v.ValueKind == JsonValueKind.String))
            {
                if (v.TryGetDecimal(out var d)) { score = d; break; }
            }

        return (root.GetRawText(), resumen, goNoGo, score);
    }

    /// <summary>Hash corto para logs (tolerante a hashes de prueba cortos).</summary>
    internal static string HashCorto(string hash) => hash.Length >= 12 ? hash[..12] : hash;

    internal static JsonElement? ParseResultado(string? json)
    {
        if (string.IsNullOrWhiteSpace(json)) return null;
        try { return JsonDocument.Parse(json).RootElement.Clone(); }
        catch { return null; }
    }

    /// <summary>Prompt del análisis comercial (adaptado de la base PRJ-001: RFP_DATA_EXTRACTION + analisis-tender).</summary>
    internal static string PromptAnalisisComercial(int documentCount)
    {
        var contexto = documentCount > 1
            ? $"Se te están proporcionando {documentCount} documentos de la MISMA licitación (pliego: bases administrativas, técnicas, preguntas y respuestas, anexos). Trátalos como un conjunto y sintetiza la información de TODOS en UN ÚNICO objeto JSON — nunca respondas con un array."
            : "Se te está proporcionando el documento (pliego) de una licitación.";

        return $$"""
            Eres un analista comercial senior de TIVIT (empresa de tecnología: cloud, ciberseguridad, data center, telecomunicaciones, servicios gestionados).
            {{contexto}}

            Analiza la licitación chilena de Mercado Público y extrae la información relevante para decidir si TIVIT puede y le conviene ofertar.

            RESPONDE SOLO CON UN ÚNICO OBJETO JSON VÁLIDO con esta estructura:

            {
              "identificacion": {
                "nombre_licitacion": string | null,
                "codigo_licitacion": string | null,
                "organismo_demandante": string | null,
                "unidad_tecnica": string | null,
                "tipo_licitacion": string | null
              },
              "montos_y_duracion": {
                "monto_estimado": number | null,
                "moneda": string | null,
                "duracion_meses": number | null,
                "renovacion": boolean | null,
                "presupuesto_publicado": boolean
              },
              "fechas_clave": {
                "fecha_publicacion": string | null,
                "fecha_cierre": string | null,
                "fecha_apertura_tecnica": string | null,
                "fecha_apertura_economica": string | null,
                "fecha_estimada_adjudicacion": string | null
              },
              "criterios_evaluacion": [
                {"nombre": string, "ponderacion_porcentaje": number | null, "descripcion": string | null}
              ],
              "requisitos_administrativos": {
                "documentos_obligatorios": [string],
                "garantias": [string],
                "seguros": [string]
              },
              "requisitos_tecnicos": {
                "certificaciones_requeridas": [string],
                "experiencia_minima": string | null,
                "personal_requerido": [string],
                "infraestructura_requerida": [string]
              },
              "alcance_servicio": {
                "descripcion": string | null,
                "nivel_servicio_sla": string | null,
                "entregables": [string]
              },
              "condiciones_especiales": {
                "condiciones_pago": string | null,
                "penalizaciones": string | null,
                "subcontratacion_permitida": boolean | null,
                "clausulas_especiales": [string]
              },
              "riesgos": [
                {"categoria": string, "severidad": "alta"|"media"|"baja", "descripcion": string}
              ],
              "match_tivit": {
                "requisitos_clave": [string],
                "brechas_detectadas": [string],
                "puede_ofertar": "si"|"parcial"|"no",
                "observaciones": string | null
              },
              "estimacion": {
                "monto_referencial": number | null,
                "moneda": string | null,
                "supuestos": [string],
                "nota": "ESTIMACIÓN ORIENTATIVA — no reemplaza el análisis de costos comercial"
              },
              "go_no_go": "strong_go"|"go"|"no_go"|"strong_no_go",
              "score_confianza": number,
              "resumen_ejecutivo": "3-5 líneas ejecutivas: qué pide, cuánto, cuándo, qué requisitos clave y si TIVIT puede ofertar",
              "justificacion": "razones principales de la recomendación"
            }

            REGLAS:
            - Solo datos que estén en los documentos. Campo sin información → null (o lista vacía).
            - Cita cifras exactas cuando aparezcan (montos, fechas, plazos).
            - "go_no_go" es una RECOMENDACIÓN de la IA; la decisión final es humana. Sé conservador: si falta información crítica, baja el score.
            - "estimacion" es orientativa: indica supuestos explícitos y marca la nota textual tal cual.
            - "score_confianza": número entre 0 y 1 (confianza en la recomendación).
            RESPONDE SOLO CON JSON VÁLIDO. No uses markdown ni fences.
            """;
    }
}
