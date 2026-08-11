namespace MPM.Shared.Services;

/// <summary>
/// Modelos neutrales de la abstracción de proveedor de IA (033-migracion-qwen-g4).
/// Los servicios de dominio construyen <see cref="LlmRequest"/> sin conocer el proveedor;
/// cada implementación de <see cref="ILlmClient"/> lo traduce a su formato nativo
/// (Gemini contents[] / OpenAI-compatible messages[]).
/// </summary>

/// <summary>Parte de contenido de un mensaje.</summary>
public abstract record LlmPart;

/// <summary>Parte de texto plano (prompt, instrucciones, historial).</summary>
public sealed record LlmTextPart(string Text) : LlmPart;

/// <summary>
/// Parte de documento PDF. Si <paramref name="GcsUri"/> no es null, el proveedor puede
/// referenciar el archivo directamente (solo el camino Gemini soporta gs://); si es null,
/// el proveedor envía los bytes inline (base64).
/// </summary>
public sealed record LlmPdfPart(byte[] PdfBytes, string FileName, string? GcsUri) : LlmPart;

/// <summary>
/// Mensaje del historial/request. Rol neutral: "user" | "assistant".
/// Cada proveedor traduce a su rol nativo (Gemini: "model"; OpenAI: "assistant").
/// </summary>
public sealed record LlmMessage(string Role, IReadOnlyList<LlmPart> Parts);

/// <summary>
/// Request neutral de generación de contenido.
/// </summary>
/// <param name="Messages">Mensajes (el último suele ser el user con el prompt + documentos).</param>
/// <param name="SystemInstruction">Instrucción de sistema opcional.</param>
/// <param name="Temperature">Temperatura de generación.</param>
/// <param name="MaxOutputTokens">Presupuesto de salida (default: 65536 validado en producción).</param>
/// <param name="JsonResponse">Si true, el proveedor debe forzar salida JSON (responseMimeType / response_format).</param>
public sealed record LlmRequest(
    IReadOnlyList<LlmMessage> Messages,
    string? SystemInstruction = null,
    double Temperature = 0.2,
    int MaxOutputTokens = 65536,
    bool JsonResponse = false);

/// <summary>Resultado de generación, neutral al proveedor.</summary>
public sealed record LlmResult(
    string Text,
    string RawResponse,
    LlmUsage Usage,
    string FinishReason = "");

/// <summary>Conteo de tokens si el proveedor lo expone (puede ser 0/nullable).</summary>
public sealed record LlmUsage(
    long? PromptTokenCount = 0,
    long? CandidatesTokenCount = 0,
    long? TotalTokenCount = 0);

/// <summary>
/// El proveedor respondió sin contenido generado (choices/candidates vacío o body anómalo).
/// Caso esperable y recuperable (reintentar o degradar), no un error interno del sistema.
/// Es la base de <see cref="GeminiRespuestaBloqueadaException"/> (033-migracion-qwen-g4).
/// </summary>
public class LlmRespuestaBloqueadaException(string message, string rawResponse) : Exception(message)
{
    public string RawResponse { get; } = rawResponse;
}
