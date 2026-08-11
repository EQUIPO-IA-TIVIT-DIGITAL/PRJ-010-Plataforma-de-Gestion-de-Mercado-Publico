# BenchmarkLlm — Harness de benchmark de proveedores de IA

**Spec**: `specs/033-migracion-qwen-g4/spec.md` (US2) | **2026-08-11**

Compara la calidad de extracción JSON del análisis de licitaciones entre **Gemini 2.5 Pro**
(Vertex AI) y un proveedor **OpenAI-compatible** (Qwen 3.7 G4, vLLM/Ollama/llama.cpp) usando
**el mismo prompt de producción** (`GeminiService.GetAnalisisPrompt`), el mismo request neutral
(`LlmRequest`) y los mismos clientes de `MPM.Shared`. Emite un informe markdown con veredicto
go/no-go contra el umbral acordado (**≥ 90% de campos críticos idénticos**).

## Uso

```bash
dotnet run --project tools/BenchmarkLlm -- \
  --docs <archivo-o-dir> \
  --gemini-project <projectId> \
  [--gemini-region us-central1] \
  --qwen-endpoint http://localhost:8000/v1 \
  --qwen-model qwen3.7-g4 \
  [--qwen-key <api-key>] \
  [--max-docs 10] \
  [--salida benchmark-qwen-g4.md]
```

## Argumentos

| Argumento | Obligatorio | Descripción |
|-----------|-------------|-------------|
| `--docs` | Sí | Directorio con PDFs, un `.pdf` solo, o un `.txt` con rutas (una por línea) |
| `--gemini-project` | Sí | Project ID de GCP para Vertex AI (ADC) |
| `--gemini-region` | No | Región de Vertex (default `us-central1`) |
| `--qwen-endpoint` | Sí | Base URL del servidor Qwen (ej. `http://host:8000/v1`) |
| `--qwen-model` | Sí | Id del modelo (ej. `qwen3.7-g4`) |
| `--qwen-key` | No | API key del servidor (on-premise puede no requerirla) |
| `--max-docs` | No | Límite de documentos (default 10) |
| `--salida` | No | Archivo del informe (default `benchmark-qwen-g4.md`) |

## Requisitos

1. **Autenticación ADC de Google** (camino Gemini): `gcloud auth application-default login`
   con permisos `aiplatform.user` sobre el project.
2. **Servidor Qwen accesible** (camino Qwen) con la API OpenAI-compatible del contrato
   `specs/033-migracion-qwen-g4/contracts/openai-compat-api.md`.
3. **Documentos reales de licitaciones FUERA del repo** (lección aprendida: un `benchmark/`
   con documentos reales se removió del repo en 2026). El informe se archiva en `docs/` o
   fuera del repo; los PDFs nunca se commitean.

## Criterio de muestra

- ≥ 10 documentos (o el máximo disponible).
- Incluir: un PDF **escaneado** (sin capa de texto) y un **workspace multi-documento**
  (para validar el flujo de revocación) si están disponibles.

## Salida del informe

- Resumen: JSON válido, truncamientos (`finish_reason=length`), latencia p50/p95 por proveedor.
- Paridad de **campos críticos** por documento (montos, fechas, puntuaciones, resultado,
  criterios/contadores) con estados: igual / diferente / solo-en-un-proveedor.
- Veredicto automatizado **GO/NO-GO** contra el umbral 90% + lista de discrepancias ordenadas
  por criticidad (montos y criterios primero) para revisión manual.

## Notas

- Los clientes se construyen directos (sin DI ni resolver) para aislar la comparación.
- La ejecución real del benchmark (evidencia de go/no-go) queda pendiente de la URL del
  servidor Qwen y de las credenciales ADC del entorno; ver `tasks.md` T027.
