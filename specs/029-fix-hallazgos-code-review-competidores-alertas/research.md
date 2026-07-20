# Research: Corrección de hallazgos de code review + QA

## FR-001 — `codigo_estado` se resetea a 1 en vez de preservar el existente

**Decisión**: Cambiar, en el branch `ON CONFLICT DO UPDATE` de `usp_SyncEngine_MergeLicitaciones` (definido en `V106__Protect_MergeLicitaciones_rich_data.sql`), la expresión de `codigo_estado` de `COALESCE((SELECT codigo FROM estados_licitacion WHERE codigo = EXCLUDED.codigo_estado), 1::SMALLINT)` a `COALESCE((SELECT codigo FROM estados_licitacion WHERE codigo = EXCLUDED.codigo_estado), licitaciones.codigo_estado)` — mismo patrón que ya usan `descripcion`/`organismo`/`monto_estimado` en la misma migración. El branch `INSERT` (licitación nueva, sin fila previa que preservar) mantiene el fallback a `1` sin cambios — para una fila nueva no hay "estado existente" que perder, y el código `1` ya queda excluido del catálogo público (`usp_Catalogos_EstadosLicitacion`, `V108`), cumpliendo el requisito de "quedar marcado de forma distinguible" del edge case de la spec.
>
> **Corrección tras verificar el código**: la primera versión de este research asumía una "tabla de errores por-item introducida en V096" que no existe — se verificó `V088__Fix_usp_SyncEngine_MergeLicitaciones_per_item_errors.sql` (el archivo real de ese cambio, no V096) y el mecanismo real es el parámetro `OUT p_error_msg` (texto, tope 4000 caracteres) que `SyncEngineHandler.cs:34-36` ya lee y loguea como `logger.LogWarning` en cada sync. Se reusa ese mismo mecanismo (no una tabla) para auditar el caso de `codigo_estado` no reconocido en un `UPDATE`: se calcula si el código entrante resolvió contra el catálogo antes del `ON CONFLICT`, y si no, se agrega una entrada a `p_error_msg` con el mismo formato que ya usa el bloque `EXCEPTION WHEN OTHERS` (`codigo_externo || ': ' || mensaje`), sin abortar ni excluir esa licitación del conteo de actualizados.

**Rationale**: Es el mismo patrón ya validado en la misma migración para las otras columnas protegidas, y el mecanismo de auditoría reusa exactamente lo que `SyncEngineHandler.cs` ya lee y loguea por cada sync — no requiere tocar C# ni agregar una tabla nueva.

**Alternativas consideradas**:
- Agregar una columna `codigo_estado_no_reconocido BOOLEAN` a `licitaciones`: descartado, el `p_error_msg` ya cumple el mismo propósito de auditoría sin cambio de esquema.
- Rechazar el registro completo del sync si `codigo_estado` es inválido: descartado, es más agresivo de lo que pide la spec (que solo pide no perder el estado válido existente, no bloquear el resto del sync de esa licitación).

---

## FR-002 — Búsqueda NL hardcodea `fecha_desde` a 2026-01-01

**Decisión**: `ConsultaSemanticaResult.FechaDesde` ya existe y ya se calcula en `ConsultaSemanticaService` (línea 139) — el bug es que `LicitacionService.BuscarNaturalAsync` nunca lo lee ni lo pasa a `LicitacionHandler.BuscarNaturalAsync`, que a su vez no tiene el parámetro. Se agrega el parámetro `fechaDesde` a `LicitacionHandler.BuscarNaturalAsync` (mismo patrón que el `fechaHasta` ya wireado) y se pasa `interpretacion?.FechaDesde` desde `LicitacionService`, análogo a como ya se pasa `FechaHasta` en la línea 122.
>
> **Actualización tras implementar**: no bastaba con dejar de hardcodear el valor en C# — `usp_Licitaciones_BuscarNatural`/`_Count` (V107) también hardcodeaban `p_fecha_desde DATE DEFAULT '2026-01-01'` a nivel SQL y lo usaban en el `WHERE` sin el guard `p_fecha_desde IS NULL OR ...` que `p_fecha_hasta` sí tiene — pasar `NULL` habría excluido casi todos los resultados. Se agregó `V110__Fix_BuscarNatural_FechaDesde_Opcional.sql` para corregir ambas funciones con el mismo guard, verificado con datos reales vía Docker (ver `tasks.md`, T009).

**Rationale**: El dato ya se calcula correctamente vía Gemini — es un fix de threading de parámetro, no una feature nueva. Sigue el mismo patrón que `fechaHasta`, que sí quedó bien conectado en el mismo PR.

**Alternativas consideradas**:
- Eliminar el filtro de fecha por completo de `BuscarNaturalAsync`: descartado, pierde la capacidad de acotar por fecha que la spec 018 sí pedía.
- Cambiar el hardcode a `DateTime.MinValue`/`null` sin usar la inferencia de Gemini: descartado, resuelve el síntoma (deja de bloquear resultados viejos) pero no aprovecha el dato que Gemini ya infiere cuando el usuario sí menciona una fecha en la consulta.

---

## FR-003 — Excepción no controlada si Gemini bloquea la respuesta

**Decisión**: Envolver el parseo de `candidates[0]` en `CompetidorGeminiService` con una validación explícita (`candidates.GetArrayLength() == 0` → lanzar una excepción de dominio tipada, ej. `GeminiRespuestaBloqueadaException`) y capturarla en `CompetidorAnalysisService.ObtenerOGenerarAnalisisAsync` para traducirla a un resultado de error manejado que `CompetidoresController` devuelve como 422 (no 500) con un mensaje claro. Ver `contracts/competidores-analisis-api.md`.

**Rationale**: Distinguir "Gemini rechazó el contenido" (esperable, recuperable, el usuario puede reintentar o el caso es simplemente no analizable) de un error interno real (500) es consistente con cómo `GeminiService` en Análisis ya maneja sus propios casos de fallo — no se inventa un mecanismo nuevo, se aplica el que ya existe en el módulo hermano.

**Alternativas consideradas**:
- Capturar genéricamente cualquier excepción en el controller y devolver 500 con mensaje genérico: descartado, no permite al frontend distinguir "reintentable" de "error real del sistema".

---

## FR-004 — `ActualizarLicitacionEnCalienteAsync` sin guard de borrado lógico

**Decisión**: Agregar `AND deleted_at IS NULL` al `WHERE codigo_externo = @codigoExterno` del UPDATE en `AlertasHandler.cs`, igual que ya tiene la query equivalente en `LicitacionHandler.cs:191`.

**Rationale**: Es la misma invariante que el resto del sistema ya respeta; el hallazgo es una omisión puntual, no una decisión de diseño distinta.

**Alternativas consideradas**: Ninguna — es un fix directo de una línea, no hay trade-off que evaluar.

---

## FR-005 — Parseo de tabla de ofertas por posición fija sin validar orden

**Decisión**: Antes de destructurar `celdas`, mapear el índice real de cada columna (`rut`, `proveedor`, `montoTexto`, `estado`) buscando su header por texto (mismo mecanismo que ya usa la función para *localizar* la tabla completa, solo que aplicado también a las columnas individuales) en vez de asumir posiciones fijas. Si algún header esperado no se encuentra, la fila se marca como no reconocida y se omite (loggeada), en vez de asignar valores a la columna incorrecta.

**Rationale**: Reutiliza el mismo mecanismo de detección de headers que la función ya usa para encontrar la tabla — no introduce una técnica nueva, solo la aplica también a nivel de columna.

**Alternativas consideradas**:
- Validar `celdas.length === 5` estrictamente: descartado, no protege contra un reordenamiento de columnas (mismo largo, distinto orden), que es el caso real que motivó el hallazgo.

---

## FR-006 — Cliente Gemini duplicado entre Análisis y Competidores (incluye el límite de tokens plausible)

**Decisión**: Extraer `VertexGeminiClient` a `MPM.Shared/Services/`, con el armado de request (`generationConfig`, endpoint, token ADC) y el parseo de respuesta (extracción de `candidates[0].content.parts[0].text`, incluyendo el guard de FR-003, y el strip de fences markdown que `GeminiService` ya tiene) centralizados ahí. `maxOutputTokens` queda como parámetro del cliente, no hardcodeado por caller — `GeminiService` sigue usando `65536` (ya validado en producción) y `CompetidorGeminiService` pasa a usar el mismo valor por defecto salvo que se justifique un valor distinto.

**Rationale**: Es la única forma de que el fix del límite de tokens (y cualquier fix futuro de manejo de errores/reintentos de Vertex AI) no tenga que aplicarse dos veces. `MPM.Shared` es explícitamente el lugar señalado por la constitución (Principio I) para lo compartido entre módulos.

**Alternativas consideradas**:
- Dejar la duplicación y solo subir el número en `CompetidorGeminiService`: descartado, no resuelve la causa raíz (el hallazgo original de "duplica en vez de reusar"), y dejaría el próximo fix de Vertex AI expuesto al mismo riesgo de desincronización.
- Mover el cliente a `MPM.Modules.Analisis` y que Competidores lo referencie: descartado, viola el Principio I ("los módulos solo pueden referenciar `MPM.Shared` y `MPM.Core`; nunca a otro módulo").

---

## FR-009 (QA BUG-002) — Filtro de fecha normal de Licitaciones da 500

**Decisión**: En `LicitacionHandler.ListarAsync` (líneas 22-37), reemplazar el objeto anónimo `new { ..., p_fecha_desde = fechaDesde, p_fecha_hasta = fechaHasta, ... }` por `DynamicParameters` con `DbType.Date` explícito para ambos, siguiendo exactamente el mismo patrón que ya usan `BuscarNaturalAsync` (líneas 214/218/228/232) y `ActualizarDetalleAsync` (que documenta el porqué en su propio comentario: sin el tipo explícito, un `DateTime?` en `null` o un valor real llega a Postgres como parámetro "unknown" y la función `usp_Licitaciones_Listar` — que declara `p_fecha_desde DATE`/`p_fecha_hasta DATE` en V093 — no resuelve el overload, error 42883). En el frontend (`LicitacionesPage.tsx`), el manejo de error de la query de listado debe distinguir un 500 real de una respuesta vacía legítima, mostrando un mensaje de error visible en el primer caso.

**Rationale**: Verificado directamente contra el código (`LicitacionHandler.cs:22-37` vs `:196-234`) — es el mismo bug que el propio código ya documentó y corrigió en otros 3 lugares del mismo archivo, solo que `ListarAsync` quedó afuera. No es una hipótesis de QA sin verificar: se confirmó que `usp_Licitaciones_Listar` (V093) efectivamente declara `DATE`, y que `ListarAsync` es el único método de fecha en este archivo que no tipa explícitamente sus parámetros de fecha.

**Alternativas consideradas**:
- Cambiar la firma del stored procedure para aceptar `TIMESTAMP` en vez de `DATE`: descartado, más invasivo (requiere migración de esquema) para resolver algo que ya tiene un patrón de fix conocido y probado en el mismo archivo.
- Solo arreglar el backend sin tocar el frontend: descartado, la spec exige explícitamente (edge case) que un error real deje de verse igual que "sin resultados" — quedaría el mismo problema de UX que hizo que este bug pasara desapercibido en QA hasta ahora.

**Nota de agrupación**: se implementa junto con FR-002 (mismo archivo, mismo tipo de bug — parámetro de fecha sin tipar) para evitar dos cambios separados tocando las mismas líneas de `LicitacionHandler.cs`.

---

## SC-008 (QA BUG-001) — Filtro "Estado" duplicado: verificación de regresión, no fix nuevo

**Decisión**: No se escribe código nuevo. Se agrega un caso de verificación explícito en `quickstart.md` que confirma que `V108__Reconciliar_Catalogo_Tipos_Estados.sql` (ya presente en esta rama, no parte de este plan) efectivamente resuelve el bug reportado por QA: `usp_Catalogos_EstadosLicitacion()` ya filtra `WHERE e.codigo IN (5, 6, 7, 8, 15)`, y `LicitacionFilterBar.tsx:25` consume ese catálogo sin lista hardcodeada propia.

**Rationale**: QA testeó contra un estado del sistema que aparentemente no incluía `V108` todavía (su nota solo referencia `V086`). Verificar en vez de re-implementar evita trabajo duplicado sobre algo que el propio code review de esta rama ya covirtió en hecho consumado. Si la verificación en el entorno real de QA falla, el edge case de la spec ya deja claro que se reclasifica como bug abierto (prioridad Alto, como lo reportó QA), no se cierra por asunción.

**Alternativas consideradas**: Filtrar también en el frontend (`LicitacionFilterBar.tsx`) como defensa adicional: descartado por ahora — sería una segunda capa de protección redundante mientras la verificación de backend no falle; se reconsideraría solo si SC-008 no pasa en la validación real.

---

## FR-007 — Monto $0 se muestra como dato faltante

**Decisión**: Cambiar la condición de render de `v ? ... : '—'` a `v !== null && v !== undefined ? ... : '—'`.

**Rationale**: Fix directo de un solo carácter de lógica; no hay alternativa de diseño que evaluar.

---

## FR-008 — Filtro "solo ofertadas" perdido en el scraper v1 deprecado (alcanzable solo por fallback)

**Decisión**: Según la spec, no se modifica código de `tools/scraper-mp` (v1, deprecado). Se corrige `MpSessionProvider.cs` para que su fallback hardcodeado apunte a `tools/scraper-mp-v2/exportar-sesion.js` (el mismo binario que ya usa Docker vía `docker-compose.yml`), no a `tools/scraper-mp`, de modo que un entorno sin `Extraccion:ExportarSesionScriptPath` configurado no pueda ejecutar accidentalmente el código deprecado con el bug del filtro.

**Rationale**: Cierra el único camino real por el que el hallazgo es alcanzable en producción, sin gastar esfuerzo manteniendo dos scrapers en paralelo.

**Alternativas consideradas**: Aplicar el mismo fix de re-aplicación de filtro a v1: descartado por la spec — v1 está deprecado y v2 ya lo resuelve; mantenerlo al día sería esfuerzo desperdiciado en código que no debería ejecutarse.

---

## FR-010 (QA BUG-003) — Import histórico masivo sin tipo/organismo real

**Decisión**: Dos pasos. (1) Re-derivar `tipo` para los ~124.887 registros afectados a partir del sufijo del `codigo_externo` (patrón ya observable: LE/LP/LQ/LR/CO/CA/TD/etc., el mismo glosario que ya usa `V108`/spec `026`) — es determinístico, no requiere llamada externa. (2) Para `organismo` (y cualquier campo que el sufijo no resuelva), ejecutar como job de backfill el mismo mecanismo que `LicitacionService.ObtenerPorCodigoAsync` ya usa al abrir el detalle (`apiMpService.GetDetalleAsync` → `ActualizarDetalleAsync`), iterado sobre los registros con `Descripcion` vacía y `FechaPublicacion` nula (la misma condición que ya dispara el auto-fix individual), en vez de depender de que cada uno se abra manualmente. Se corre como script/job idempotente, no como migración SQL (llama a una API externa, no pertenece a `Database/Scripts/`).

**Rationale**: Reusa dos mecanismos que el propio sistema ya tiene validados en producción (el glosario de sufijos de `026`/`V108`, y el enriquecimiento on-demand de `ObtenerPorCodigoAsync`) en vez de construir un tercero. El backfill es idempotente porque la misma condición (`Descripcion` vacía + `FechaPublicacion` nula) deja de cumplirse una vez que un registro se corrige, así que reintentarlo no causa doble trabajo.

**Alternativas consideradas**:
- Re-scrapear las 126k licitaciones: descartado, es la opción más costosa y el dato ya es recuperable sin volver a tocar el portal para la mayoría de los casos (sufijo + API real).
- Solo re-derivar `tipo` desde el sufijo, dejar `organismo` como deuda pendiente: descartado, la spec (US6) exige ambos; el mecanismo de backfill ya cubre los dos en la misma pasada.

---

## FR-011 + FR-012 (QA BUG-005 y BUG-010) — "Analizar todo" solo procesa 1 documento / no detecta revocación

**Decisión**: Se abordan juntos porque comparten causa raíz: `AnalisisService.cs:91-97` (confirmado por lectura directa) — el caso sin `documentoId` explícito solo hace `doc = await _handler.ObtenerDocumentoAsync(docList.First().Id, ct)`, un único documento. Se reemplaza por: enviar **todos** los documentos del workspace a Gemini en una sola llamada (Gemini soporta múltiples archivos adjuntos por request, ya usado en el patrón de PDFs de `GeminiService`), pidiendo en el prompt que identifique explícitamente relaciones de precedencia/revocación entre documentos del mismo workspace (ej. "¿algún documento indica que otro documento anterior queda sin efecto?") y que lo refleje en el resultado. Si el volumen de documentos hace inviable una sola llamada (límite de tokens), la alternativa es consolidar N análisis individuales en un solo resultado vía un paso de síntesis adicional — a decidir en implementación según el límite real observado, no en esta fase de research.

**Rationale**: Es la misma llamada a Gemini la que puede identificar tanto "qué dice cada documento" como "qué documento revoca a cuál", si se le da el contexto de todos a la vez — separar esto en dos mecanismos distintos duplicaría el costo de tokens sin necesidad. `AnalisisService.cs:91-97` es el único punto de la causa raíz de ambos bugs (BUG-005 y BUG-010 son, en esencia, la misma falta de contexto multi-documento).

**Alternativas consideradas**:
- Solo renombrar el botón a "Analizar documento más reciente" sin implementar síntesis real (opción de degradación honesta que la spec permite como FR-011 alternativo): se documenta como fallback aceptable si la síntesis multi-documento real resulta inviable en el tiempo de esta spec, pero no es la primera opción — no resuelve BUG-010 (revocación), que sí requiere ver más de un documento a la vez.
- Implementar detección de revocación como una regla separada basada en fechas/metadatos (sin usar Gemini): descartado, la revocación es una declaración textual explícita dentro del documento ("DÉJESE sin efecto..."), no algo inferible solo de metadatos.

---

## FR-013 + FR-014 + FR-015 (QA BUG-008, BUG-009, BUG-006) — Extracción de Gemini: moneda, admisibilidad, monto estimado

**Decisión**: Los tres son ajustes al prompt de extracción de `GeminiService`/`AnalisisService` (no requieren cambio de esquema):
- **Moneda (FR-013)**: el prompt debe pedir explícitamente identificar el símbolo/moneda asociado a cada cifra en el texto fuente (CLP vs USD) y devolverlo como campo estructurado junto al monto, en vez de que el frontend/backend asuma dólares por defecto. Si el documento no indica moneda explícita, el campo queda como "no determinada" (edge case de la spec), no asumida.
- **Admisibilidad (FR-014)**: el prompt debe distinguir explícitamente "declarado inadmisible por el documento" de "sin puntaje/monto visible en esta sección" — probablemente el modelo hoy trata la ausencia de datos visibles como señal de inadmisibilidad. Se agrega una instrucción explícita de no inferir inadmisibilidad por ausencia de datos, solo por declaración textual.
- **Monto estimado (FR-015)**: el prompt debe diferenciar explícitamente "monto estimado/presupuesto del organismo" (fijado antes de recibir ofertas) de "monto ofertado por cada participante", con ejemplos de dónde suele aparecer cada uno en un documento típico de Mercado Público.

**Rationale**: Los tres bugs comparten el mismo patrón de causa raíz (el modelo toma el primer valor relevante que encuentra sin verificar a qué concepto corresponde exactamente) — se resuelven con el mismo tipo de intervención (prompt más explícito + verificación estructurada de la respuesta), agrupados aquí por eficiencia de implementación aunque sean 3 FRs distintos en la spec.

**Alternativas consideradas**:
- Post-procesar el resultado con reglas heurísticas en C# (ej. "el monto estimado nunca debe ser igual a un monto ofertado"): descartado como mecanismo principal — enmascararía el síntoma sin corregir la extracción, y podría descartar coincidencias legítimas (spec exige que un valor idéntico real no se altere). Se puede usar como *validación* adicional de sanity-check, no como fuente de verdad.

---

## FR-016 (QA BUG-004) — Notificación de análisis completado no es global

**Decisión**: Mover el seguimiento de transición de estado (`prevEstadoRef`, hoy un `useRef` local a `AnalisisWorkspacePage.tsx:20,31-51`, confirmado por lectura directa) a un mecanismo a nivel de aplicación — mismo patrón que ya usa `NotificationBell.tsx` (confirmado que existe en `src/mpm-web/src/components/`), que hace polling/tracking a nivel de `AppLayout` en vez de estar acoplado a una página específica.

**Rationale**: Es exactamente el patrón que el propio código base ya usa para notificaciones globales — no se introduce una técnica nueva, se aplica la que ya existe para el caso de Análisis. El `duration: 0` (no autocierre) ya es intencional y no requiere cambio.

**Alternativas consideradas**: WebSocket/SignalR dedicado para este caso: descartado, es sobre-ingeniería para lo que ya resuelve un polling a nivel de `AppLayout` como `NotificationBell.tsx` — no hay indicios de que el polling actual sea insuficiente en latencia.

---

## FR-017 (QA BUG-007) — Inconsistencias de formato y coherencia interna

**Decisión**: Normalizar el formato de moneda en el backend (un solo formateador antes de exponer el JSON al frontend) en vez de confiar en el texto libre que genera el modelo. Renombrar las métricas con nombres menos ambiguos entre sí (ej. "Diferencia vs. monto adjudicado propio" vs. "Diferencia vs. oferta del competidor"). El badge de estado (ej. "✓ Coherente") debe derivarse del mismo dato que genera el texto de la sección, no de un valor independiente que puede desincronizarse.

**Rationale**: Consistente con FR-013/FR-014/FR-015 — no confiar en texto libre del modelo para datos que se muestran estructuradamente. Es el hallazgo de menor severidad del módulo (Medio), así que se prioriza al final del frente de Análisis.

**Alternativas consideradas**: Instruir al modelo a ser consistente vía prompt únicamente, sin normalización en backend: descartado como única medida — ya se intentó implícitamente (el prompt actual no impone reglas de formato) y es el origen del bug; normalizar en backend es más confiable que confiar en que el modelo sea consistente entre secciones generadas independientemente.

---

## FR-018 (QA BUG-011) — Filtro de año usa fecha de análisis, no fecha real de licitación

**Decisión**: En `AnalisisService.cs:186` (confirmado: `todosLosAnios.Add(r.CreadoEn.Year)`), cambiar la fuente del año a una fecha real de la licitación (fecha de adjudicación si existe, si no fecha de publicación) en vez de `CreadoEn` (fecha de creación del registro de análisis).

**Rationale**: Fix directo de una línea, mismo patrón de "usar la fecha real del dato, no la fecha de cuándo se procesó" que FR-002/FR-009 ya aplican en Licitaciones — es la misma clase de bug repetida en otro módulo.

**Alternativas consideradas**: Agregar ambos años (creación y real) como filtros separados: descartado, la spec (US14) solo pide que el año real esté disponible como opción, no duplicar la UI con un filtro adicional no solicitado.

---

## FR-019 (QA BUG-012) — No se puede crear conversación directa

**Decisión**: Confirmado por lectura directa (`CrearConversacionModal.tsx:49-52` y `:90`): el `useMemo` que calcula `selectValue` retorna `undefined` en ambas ramas, y ese valor se pasa explícitamente como `value={selectValue}` al `Select` que ya está envuelto en un `Form.Item name="participanteIds"`. Se elimina el `useMemo` y el prop `value={selectValue}` por completo, dejando que `Form.Item` maneje el valor del `Select` nativamente (que es como ya funciona para el modo `grupal`, donde no hay ningún `value` explícito compitiendo).

**Rationale**: Es la causa raíz exacta, confirmada leyendo el archivo — no una hipótesis. Eliminar el código roto en vez de "arreglarlo" (ej. hacer que el `useMemo` devuelva el valor real) es más simple porque `Form.Item` ya gestiona ese valor correctamente sin necesidad de ningún estado derivado adicional.

**Alternativas consideradas**: Corregir el `useMemo` para que devuelva el valor real seleccionado en vez de `undefined`: descartado, sería reintroducir un estado derivado redundante que ya gestiona `Form.Item` — el `useMemo` no cumple ningún propósito real una vez corregido, así que eliminarlo es la opción más simple que resuelve lo mismo.
