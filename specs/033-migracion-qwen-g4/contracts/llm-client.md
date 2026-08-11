# Contrato: ILlmClient (abstracción del proveedor de IA)

**Spec**: `specs/033-migracion-qwen-g4/spec.md` | **Date**: 2026-08-11
**Ubicación propuesta**: `src/MPM.Shared/Services/ILlmClient.cs` + `LlmModels.cs`

## Propósito

Permitir que los servicios de dominio (Análisis, Competidores, Licitaciones, Alertas) invoquen al modelo de IA sin conocer el proveedor. El proveedor activo se elige por configuración (`AI:Provider`) en el punto de composición (`Program.cs`).

## Contrato

```csharp
public interface ILlmClient
{
    /// <summary>Nombre del modelo resuelto (se persiste en modelo_usado).</summary>
    string ModelName { get; }

    /// <summary>
    /// Envía un request neutral al proveedor y devuelve la respuesta parseada.
    /// </summary>
    Task<LlmResult> GenerarContenidoAsync(LlmRequest request, CancellationToken ct = default);
}
```

### Modelos de datos

```csharp
public sealed record LlmRequest(
    IReadOnlyList<LlmPart> Parts,   // contenido del mensaje (texto + documentos)
    string? SystemInstruction,      // opcional
    double Temperature = 0.2,       // default actual de análisis
    int MaxOutputTokens = 65536);   // presupuesto validado en producción (VertexGeminiClient.DefaultMaxOutputTokens)

public abstract record LlmPart;                       // base para partes de contenido
public sealed record LlmTextPart(string Text) : LlmPart;
public sealed record LlmPdfPart(byte[] PdfBytes, string FileName, string? GcsUri) : LlmPart;
// GcsUri: referencia gs:// (solo camino Gemini); si null, el proveedor decide cómo enviar (base64)

public sealed record LlmResult(
    string Text,        // texto de la respuesta (JSON crudo para análisis)
    string RawResponse, // body completo (diagnóstico/logging)
    LlmUsage Usage);    // conteo de tokens si el proveedor lo expone (nullable por campo)

public sealed record LlmUsage(long? PromptTokenCount, long? CandidatesTokenCount, long? TotalTokenCount);
```

### Traducción por proveedor

| Elemento | Gemini (Vertex AI) | OpenAI-compatible (Qwen) |
|----------|--------------------|--------------------------|
| Auth | Bearer ADC (`GoogleAdcTokenProvider`) | `Authorization: Bearer <AI:ApiKey>` (opcional) |
| Endpoint | `{region}-aiplatform.googleapis.com/.../models/{model}:generateContent` | `{AI:Endpoint}/chat/completions` |
| Mensaje | `contents[0].parts[]` | `messages[0].content[]` |
| PDF en GCS | `fileData.fileUri = gs://...` | No soportado → alternativa base64 (D4) |
| PDF inline | `inlineData.data = base64` + mimeType | `type: "file"` con `data:application/pdf;base64,...` |
| JSON | `generationConfig.responseMimeType = "application/json"` | `response_format: { "type": "json_object" }` |
| Errores | `GeminiRespuestaBloqueadaException` + HTTP status | Mismo contrato tipado (mismas excepciones) |

## Contrato de configuración

| Variable | Requerida | Descripción |
|----------|-----------|-------------|
| `AI:Provider` | Sí | `gemini` (default) o `openai` |
| `AI:Endpoint` | Si `openai` | Base URL del servidor (ej. `http://qwen-tivit:8000/v1`) |
| `AI:Model` | Sí | Id del modelo (ej. `qwen3.7-g4`); default `gemini-2.5-pro` |
| `AI:ApiKey` | No | Token bearer para el camino `openai` |

## Reglas de registro (DI) — resolución dinámica por request

- `Program.cs` registra:
  - `ILlmClient` (impl `VertexGeminiClient`) y `ILlmClient` (impl `OpenAiCompatClient`) **por key** (`gemini` / `openai`) — ambas conviven.
  - `LlmClientResolver` (singleton en MPM.Shared): consulta el proveedor activo (**config persistida** `system_ai_provider` vía SP → cache en memoria TTL ~30s con invalidación explícita → fallback env → default `gemini`) y devuelve el `ILlmClient` correspondiente (FR-017).
- Los servicios de módulo inyectan `LlmClientResolver` y resuelven el cliente por request (`resolver.GetClient()`), nunca las clases concretas ni una instancia fija de arranque.
- Proveedor desconocido → error de configuración claro al resolver, sin dejar el sistema en estado ambiguo (US1, escenario 2).
- `AI:ApiKey` se lee por request desde config (no se cachea junto al provider, por si rota en CSMS).

## Compatibilidad / no-regresión

- Con `AI:Provider=gemini` (default), el request armado y el parseo de respuesta deben ser byte-a-byte equivalentes a los de hoy (mismo endpoint, mismo body, mismas excepciones) — verificado por la suite de tests existente + quickstart.
- `GeminiService.ModelName` deja de ser la fuente de verdad: pasa a `ILlmClient.ModelName` (resuelto desde `AI:Model`).
