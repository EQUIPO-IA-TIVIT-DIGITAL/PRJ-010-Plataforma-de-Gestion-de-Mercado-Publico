# Contrato: API OpenAI-compatible (servidor Qwen 3.7 G4)

**Spec**: `specs/033-migracion-qwen-g4/spec.md` | **Date**: 2026-08-11

## Alcance

Contrato de integración entre MPM y el servidor que sirve Qwen 3.7 cuantizado G4. El servidor es externo al repo (on-premise TIVIT, VM, o contenedor local de desarrollo) y **debe** exponer una API compatible con OpenAI Chat Completions.

## Endpoint usado por MPM

```
POST {AI:Endpoint}/chat/completions
Authorization: Bearer {AI:ApiKey}   (omitir si AI:ApiKey está vacío)
Content-Type: application/json
```

### Request (mapeo desde LlmRequest)

```jsonc
{
  "model": "{AI:Model}",                    // ej. "qwen3.7-g4"
  "messages": [
    {
      "role": "system",
      "content": "{SystemInstruction}"      // si aplica
    },
    {
      "role": "user",
      "content": [
        { "type": "text", "text": "{prompt}" },
        {
          "type": "file",                   // o "image_url" si el servidor lo exige para PDF
          "file": {
            "mime_type": "application/pdf",
            "data": "data:application/pdf;base64,{...}"
          }
        }
        // un elemento por documento del workspace (análisis multi-documento)
      ]
    }
  ],
  "temperature": 0.2,
  "max_tokens": 65536,
  "response_format": { "type": "json_object" }   // o guided_json según servidor
}
```

> **Nota de compatibilidad**: la codificación exacta de PDFs (parte `file` con `data:` URI vs `image_url` vs mensaje de texto con base64) depende del servidor (vLLM vs Ollama vs llama.cpp) y de si el modelo es multimodal. Se valida una vez en US3 con el servidor real y se fija en este contrato como norma.

### Response esperada

```jsonc
{
  "id": "...",
  "model": "qwen3.7-g4",
  "choices": [
    {
      "index": 0,
      "message": { "role": "assistant", "content": "{JSON crudo del análisis}" },
      "finish_reason": "stop"               // "length" = truncamiento → registrar y tratar
    }
  ],
  "usage": { "prompt_tokens": 0, "completion_tokens": 0, "total_tokens": 0 }
}
```

## Requisitos del servidor (para el equipo que lo provee)

1. **OpenAI-compatible** en `/v1/chat/completions` (o `{AI:Endpoint}/chat/completions`).
2. **JSON mode** habilitado (`response_format` o guided JSON); la extracción estructurada depende de ello.
3. **Ventana de contexto ≥ 70k tokens** efectivos tras cuantización: el prompt de análisis + PDFs + 65536 tokens de salida no deben truncarse en el caso nominal.
4. **Soporte PDF**: aceptar PDFs en el request (base64 data URI) y razonar sobre su contenido (incluidos escaneados con OCR de capa o no — se valida en benchmark).
5. **Tiempo de respuesta**: p95 de la llamada completa ≤ 5 minutos para un análisis multi-documento típico (a confirmar con benchmark; el flujo actual es síncrono).
6. **Estabilidad**: reintentos del lado MPM (mismo patrón de hoy); el servidor debe fallar con HTTP status estándar y body diagnóstico, nunca colgar.

## Verificación de humo (pre-benchmark)

```bash
curl -s http://<host>:8000/v1/chat/completions \
  -H "Content-Type: application/json" \
  -d '{"model":"qwen3.7-g4","messages":[{"role":"user","content":"Responde solo: OK"}],"max_tokens":10}'
# → 200 con choices[0].message.content == "OK"
```

## Errores mapeados (contrato de errores MPM existente)

| Caso | HTTP | Manejo MPM |
|------|------|-----------|
| Servidor caído / timeout | conexión falla / 504 | Error tipado de LLM, análisis queda `error` reintentable (como hoy con Gemini) |
| Respuesta sin choices | 200 con body vacío/anómalo | Misma semántica de `GeminiRespuestaBloqueadaException` → error tipado |
| JSON inválido en content | 200 | `ParseGeminiResponse` aplica tolerancia actual; si no parsea, error tipado |
| Truncamiento (`finish_reason=length`) | 200 | Se registra (ya existió bug real con Gemini); métrica del benchmark |
