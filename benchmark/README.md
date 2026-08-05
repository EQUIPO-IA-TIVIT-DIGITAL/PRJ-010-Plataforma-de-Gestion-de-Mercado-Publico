# Benchmark: Gemini vs Qwen vs Gemma

Carpeta armada para comparar el análisis de actas de evaluación entre el sistema actual
(Gemini 2.5 vía Vertex AI) y los modelos candidatos a self-hosting en el ASUS Ascent GX10
(Qwen3-VL / Qwen3.x MoE, Gemma 4).

## Contenido

- `prompt.txt` — el prompt exacto que MPM le envía hoy a Gemini para analizar un acta
  (extraído de `GeminiService.cs`, sin modificar). Usar tal cual, junto con el PDF, como
  entrada al modelo que se esté probando.
- `documentos/` — 3 actas reales, tomadas de workspaces de análisis que ya están
  `completado` en producción hoy (así hay baseline real para comparar, no solo "se ve bien"):
  - `01_ws80_ganada_resolucion-adjudicacion.pdf` — caso donde TIVIT **ganó**
  - `02_ws84_perdida_escaneado_REX-N280.pdf` — caso perdido, resolución **escaneada**
    (no texto plano) — el más exigente para OCR, donde más se va a notar la diferencia
    de calidad entre modelos
  - `03_ws81_perdida_informe-evaluacion.pdf` — caso perdido, informe de evaluación con
    texto nativo (no escaneado) — sirve de control para aislar "problema de OCR" de
    "problema de razonamiento/extracción"
- `gemini-baseline/` — el resultado real que Gemini ya generó para cada uno de estos 3
  documentos (JSON completo tal cual quedó guardado en `analisis_resultados`), sacado en
  vivo del sistema en producción. **Esta es la referencia contra la que hay que comparar
  las respuestas de Qwen y Gemma** — no evaluar en el vacío, evaluar contra esto.

## Cómo correr la comparación

Para cada documento y cada modelo (Qwen y Gemma corridos en el GX10 / Model Garden):

1. Enviar el contenido de `prompt.txt` como system/user prompt.
2. Adjuntar el PDF correspondiente como input multimodal (no pegar el texto extraído a mano —
   la idea es probar también la capacidad de lectura/OCR del modelo, no solo el razonamiento).
3. Guardar la respuesta JSON completa de cada modelo.
4. Comparar campo por campo contra el JSON correspondiente en `gemini-baseline/`.

## Qué mirar al comparar

- ¿El JSON es válido y respeta el esquema completo, o faltan campos / vienen como texto suelto?
- ¿Los montos y sus monedas están bien identificados (ver reglas críticas de MONEDA en el prompt)?
- ¿Los puntajes por criterio/subcriterio calzan con lo que dice el acta?
- Para el documento escaneado (`03_...`): ¿el modelo realmente leyó el contenido, o alucinó
  campos porque no pudo procesar la imagen?
- ¿Las conclusiones (`analisis_tivit`, `riesgos_identificados`, `recomendaciones_estrategicas`)
  tienen evidencia citada del documento, o son genéricas?
