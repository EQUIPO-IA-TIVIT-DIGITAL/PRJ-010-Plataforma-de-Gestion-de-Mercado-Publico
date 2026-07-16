# Research: Buscador Inteligente en Lenguaje Natural

**Feature**: `018-buscador-inteligente-nl` | **Fecha**: 2026-07-16

## Reanálisis del sistema: qué ya existe

Antes de decidir el enfoque técnico, se auditó el código actual para no duplicar infraestructura. Hallazgos clave:

1. **`usp_Licitaciones_BuscarNatural`** (`V067__Create_usp_BuscarNatural.sql`) ya existe end-to-end (SP → `LicitacionHandler.BuscarNaturalAsync` → `LicitacionService` → `GET /api/v1/licitaciones/buscar-natural`), pero es full-text léxico puro (`plainto_tsquery('spanish', p_query)` sobre la columna `search_vector`, indexada por `V066__Add_tsvector_search.sql`). No interpreta sinónimos ni conceptos. El hook `useBuscarNatural` (`src/mpm-web/src/hooks/useLicitaciones.ts:40-55`) ya llama a este endpoint pero **no está usado en ninguna pantalla** — es la barra a conectar, no a crear desde cero.
2. **`usp_Licitaciones_Listar`** (`V093__Fix_usp_Licitaciones_Listar_Search.sql`) ya soporta filtros estructurados robustos: `p_estado`, `p_tipo`, `p_organismo`, `p_fecha_desde`/`p_fecha_hasta`, con `websearch_to_tsquery` + índice trigram sobre `codigo_externo`. Este es el motor de filtrado a reutilizar — 018 no necesita reinventar el filtrado estructurado, solo necesita **poblar esos parámetros a partir de lenguaje natural**.
3. **No existe pgvector ni ninguna infraestructura de embeddings** en el proyecto (confirmado por grep negativo sobre todos los scripts SQL). Introducirla sería trabajo greenfield: extensión, columna vectorial, pipeline de generación/reindexado, y un proveedor de embeddings nuevo — no justificado por el volumen de datos actual (miles, no millones, de licitaciones) ni por el requisito de latencia SC-001 (<3s).
4. **Ya existe el patrón exacto necesario para interpretar lenguaje natural con IA**: `SinonimosIaService` (`src/MPM.Modules.Alertas/Services/SinonimosIaService.cs`) — llama a Gemini 2.5 Flash vía Vertex AI + ADC (`GoogleAdcTokenProvider`), pide `responseMimeType: "application/json"`, y tiene fallback silencioso (`null`) si Vertex falla o no hay `GOOGLE_CLOUD_PROJECT`. Este es el molde a replicar en `MPM.Modules.Licitaciones` (cada módulo construye su propia llamada a Gemini, Principio I de la constitución — no se referencia el servicio de otro módulo).
5. **No hay columna de ubicación/región** en `licitaciones` (`V002__Create_licitaciones.sql`) — el FR-002 del spec ("ubicación implícita") no tiene dónde aterrizar hoy. Se documenta como limitación conocida, no se agrega en este alcance (fuera de las user stories P1).
6. **No hay catálogo de rubros/sinónimos normalizado**. La columna `categoria` en `licitaciones_items` es texto libre jerárquico ("Categoría padre / Categoría hija") proveniente del scraping. No hay vocabulario base — la expansión de sinónimos depende enteramente de lo que Gemini infiera por prompt, igual que ya hace `SinonimosIaService` para las keywords de Alertas.

## Decisión: interpretación vía Gemini + tsquery enriquecido (no embeddings)

**Decisión**: Un nuevo servicio `ConsultaSemanticaService` en `MPM.Modules.Licitaciones` llama a Gemini (mismo patrón que `SinonimosIaService`) para, a partir de la consulta en lenguaje natural, extraer en una sola llamada:

### Modelo: `gemini-2.5-flash-lite`

Decisión del usuario (2026-07-16), con costos confirmados por millón de tokens:

| Modelo | Entrada | Salida |
|---|---|---|
| **gemini-2.5-flash-lite** (elegido) | USD 0.10 | USD 0.40 |
| gemini-3.1-flash-lite (alternativa) | USD 0.25 | USD 1.50 |

**Rationale**: la tarea (extraer sinónimos + filtros estructurados de una consulta corta) es simple y categórica — el mismo tipo de tarea que ya resuelve `SinonimosIaService` con `gemini-2.5-flash` (no siquiera el lite) en Alertas. `flash-lite` es el modelo más barato disponible que cubre esta categoría de trabajo; no se justifica pagar 2.5-6x más por `gemini-2.5-flash` o `gemini-3.1-flash-lite` sin evidencia de que la calidad de extracción sea insuficiente. Se descarta explícitamente Groq (más barato aún) por sensibilidad de los datos — licitaciones y patrones de búsqueda del negocio no deben salir del stack Google Cloud ya autorizado (mismo criterio que llevó a usar Vertex AI + ADC en vez de API key, ver `020-migracion-gemini-adc`).

**Plan de reevaluación**: si en validación real (quickstart, Escenario 1/2) la tasa de recall de sinónimos no alcanza SC-002 (80%), subir a `gemini-3.1-flash-lite` antes de considerar `gemini-2.5-flash` completo — es el siguiente escalón de costo, no el más caro.
- Términos expandidos (sinónimos/conceptos del dominio) para enriquecer el `tsquery`.
- Filtros estructurados detectables: `estado` (activa/cerrada/adjudicada → mapeado al código de `estados_licitacion`), rango de `monto_estimado` si la consulta lo menciona, rango de fechas si aplica.

El resultado alimenta `usp_Licitaciones_Listar` (no una nueva SP de scoring) combinando: `p_search` = términos originales + expandidos (unidos para `websearch_to_tsquery`), más los filtros estructurados ya soportados (`p_estado`, `p_fecha_desde`, `p_fecha_hasta`). Esto reutiliza el motor de ranking y paginación existente en vez de duplicarlo en `usp_Licitaciones_BuscarNatural`.

**Rationale**:
- Cero infraestructura nueva de datos (no pgvector, no tabla de embeddings, no reindexado masivo).
- Reutiliza tres piezas ya construidas y probadas en producción: autenticación ADC a Vertex, el patrón de llamada a Gemini con fallback silencioso, y el motor de filtrado/paginación de `usp_Licitaciones_Listar`.
- Cumple FR-005 (degradación controlada) de forma natural: si Gemini falla, se cae exactamente al comportamiento actual de `buscar-natural` (búsqueda literal sin expansión), sin rama de código adicional para el fallback.
- Compatible con SC-001 (<3s): una sola llamada a Gemini Flash (modelo liviano, ya usado así en Alertas) + una consulta SQL indexada, sin llamadas encadenadas.

**Alternativas consideradas**:
- **(b) Embeddings + pgvector**: descartado para este alcance — requiere extensión nueva, pipeline de generación/reindexado por cada sync diario de licitaciones, y un proveedor de embeddings (`text-embedding-004` o similar) no integrado hoy. Sería la opción correcta si SC-002 (80% recall en sinónimos) no se cumple con el enfoque léxico+IA; se deja como plan B documentado, no como trabajo de este ciclo.
- **(c) Híbrido (Gemini + pgvector)**: mismo descarte que (b), con costo aún mayor sin evidencia de que el enfoque más simple sea insuficiente.
- **Extender `usp_Licitaciones_BuscarNatural` en vez de reusar `usp_Licitaciones_Listar`**: descartado — `Listar` ya tiene más filtros estructurados (tipo, organismo, rango de fechas) y el índice trigram de `codigo_externo`; duplicar esa lógica en `BuscarNatural` es trabajo redundante. `BuscarNatural` puede quedar como el endpoint delgado que solo se encarga de interpretar y delegar.

## Filtros implícitos: alcance real vs. spec original

El spec (`spec.md`, FR-002) menciona "ubicación" como filtro implícito, pero no existe columna de ubicación en `licitaciones` hoy. Se documenta como **fuera de alcance para esta iteración** — el motor de interpretación puede detectar la intención, pero no hay campo estructurado donde aplicarla; se ignora silenciosamente en vez de fallar. Si se necesita a futuro, requiere una fase previa de extracción de región/comuna desde el organismo o las bases (no cubierto aquí).

## Migración

Última migración aplicada en el repo al momento de este research: **V106** (`V106__Protect_MergeLicitaciones_rich_data.sql`). La migración de esta feature, si se requiere (ver `data-model.md` — no se anticipa cambio de esquema, solo código), sería **V107**. Confirmar el número exacto al momento de implementar, no asumir V078 como decía la versión anterior de este plan.
