# Data Model: Corrección de hallazgos de code review + QA

Ninguno de los 18 fixes agrega tablas nuevas. Se documentan aquí las reglas de validación que cambian sobre entidades ya existentes; FR-010 puede requerir un campo/estado adicional para marcar registros "no recuperables" (ver más abajo), a confirmar en implementación.

## Licitación (`licitaciones`)

| Campo | Regla anterior (con el bug) | Regla corregida |
|---|---|---|
| `codigo_estado` | Merge de sync: si el valor entrante no matchea `estados_licitacion`, se pisa con `1` (código legado, inválido) | Merge de sync: si el valor entrante no matchea `estados_licitacion`, se conserva el `codigo_estado` existente de la fila (`COALESCE(nuevo_validado, existente)`) |
| `deleted_at` | El UPDATE de enriquecimiento en caliente (Alertas) no lo respeta — puede escribir sobre una fila soft-deleted | El UPDATE de enriquecimiento en caliente solo aplica si `deleted_at IS NULL` |
| `fecha_publicacion` (filtro `p_fecha_desde`/`p_fecha_hasta` en `ListarAsync`, QA BUG-002) | Parámetros de fecha sin `DbType.Date` explícito → Postgres no resuelve el overload de `usp_Licitaciones_Listar` (`DATE`), 500 en cada filtro de fecha | Parámetros tipados explícitamente como `DbType.Date`, mismo patrón que `BuscarNaturalAsync`/`ActualizarDetalleAsync` en el mismo archivo |
| `tipo`, `organismo` (registros del import histórico masivo, QA BUG-003) | `tipo="Licitacion"` genérico y `organismo` vacío para ~124.887 de 126.122 registros, porque el import vía `gcloud sql import csv` no pasó por `usp_SyncEngine_MergeLicitaciones` | Backfill: `tipo` re-derivado del sufijo de `codigo_externo`; `organismo` (y lo que el sufijo no resuelva) enriquecido vía el mismo mecanismo que `ObtenerPorCodigoAsync` ya usa on-demand, corrido como job sobre todos los registros con `Descripcion` vacía y `FechaPublicacion` nula |

No hay cambio de esquema para FR-001/FR-002/FR-004/FR-009 — se implementan en SQL de stored procedure / migración o en el tipeo de parámetros Dapper, sin columnas nuevas. FR-010 reusa las mismas columnas ya existentes (`tipo`, `organismo`, `descripcion`, `fecha_publicacion`, `monto_estimado`); si algún registro resulta no recuperable por ningún medio (ver Edge Cases de la spec), se evalúa en implementación si basta con dejar `tipo`/`organismo` en su valor actual con un log de auditoría, o si se justifica una columna nueva de estado de enriquecimiento — no se decide de antemano en este research para no sobre-diseñar antes de correr el backfill real y ver cuántos casos quedan sin resolver.

## Auditoría de `codigo_estado` no reconocido (parámetro `p_error_msg`, ya existente desde V088)

No es una tabla — es el parámetro `OUT p_error_msg` de `usp_SyncEngine_MergeLicitaciones` (introducido en `V088__Fix_usp_SyncEngine_MergeLicitaciones_per_item_errors.sql`), que `SyncEngineHandler.cs:34-36` ya lee tras cada sync y loguea con `logger.LogWarning` si no está vacío. Se reutiliza para registrar el caso "sync trajo un `codigo_estado` no reconocido por el catálogo" con el mismo formato que ya usa el bloque `EXCEPTION WHEN OTHERS` (`codigo_externo || ': ' || mensaje`), sin abortar el resto del lote ni excluir la licitación del conteo de actualizados.

## Consulta de búsqueda natural (`ConsultaSemanticaResult`, en memoria — no persiste)

| Campo | Estado anterior | Estado corregido |
|---|---|---|
| `FechaDesde` | Calculado por `ConsultaSemanticaService`, nunca leído por `LicitacionService`/`LicitacionHandler` | Se propaga hasta el parámetro `p_fecha_desde` de la búsqueda real; si es `null` (Gemini no infirió fecha), no se aplica filtro de fecha de inicio (comportamiento sin acotar, no un hardcode fijo) |

## Oferta (`licitaciones_ofertas`, vía scraper)

| Campo | Regla anterior | Regla corregida |
|---|---|---|
| `monto_oferta`, `estado_oferta` | Se asignan por posición fija de columna (`celdas[3]`, `celdas[4]`) sin validar que esa posición corresponda al header esperado | Se asignan por índice resuelto dinámicamente contra el header real detectado; si un header esperado no aparece, la fila se descarta y se loggea en vez de guardar datos posiblemente en la columna equivocada |
| `monto_oferta` (frontend, `CompetidoresPage.tsx`) | `0` se renderiza igual que `null` (`'—'`) | `0` se distingue de `null`/`undefined` en el render |

## Análisis de Competidor (`competidores_analisis`, vía `CompetidorGeminiService`)

| Campo/comportamiento | Estado anterior | Estado corregido |
|---|---|---|
| Manejo de respuesta Gemini sin `candidates` | Excepción no controlada → 500 genérico, sin guardar nada | Excepción de dominio (`GeminiRespuestaBloqueadaException` o equivalente) capturada en el service, traducida a un error 422 manejado (ver `contracts/competidores-analisis-api.md`); no se persiste ningún análisis parcial |
| `maxOutputTokens` de la solicitud a Gemini | Hardcodeado a `8192`, independiente del valor de `GeminiService` (`65536`) | Delegado a `VertexGeminiClient` (nuevo, en `MPM.Shared`), mismo valor por defecto que `GeminiService` salvo justificación explícita |

No se agregan entidades nuevas al dominio de Competidores — el fix es de comportamiento (manejo de error, límite de tokens) sobre el flujo ya existente.

## Catálogo de estados de licitación (`estados_licitacion`, vía `usp_Catalogos_EstadosLicitacion`, QA BUG-001)

| Campo/comportamiento | Estado reportado por QA | Estado verificado en esta rama |
|---|---|---|
| Resultado de `usp_Catalogos_EstadosLicitacion()` | Devolvía los 4 códigos legado (1-4, inventados) además de los 5 reales, duplicando el dropdown "Estado" del frontend | `V108` ya filtra `WHERE e.codigo IN (5, 6, 7, 8, 15)` — no requiere cambio adicional, solo verificación (SC-008) |

No se toca la tabla `estados_licitacion` — las filas 1-4 se dejan intactas (decisión ya documentada en `V086`/`V108`), solo se filtran al consultarlas.

## Workspace de Análisis / Documento (dominio `MPM.Modules.Analisis`)

| Campo/comportamiento | Estado anterior (con el bug) | Estado corregido |
|---|---|---|
| "Analizar todo" (sin `documentoId` explícito) | `AnalisisService.cs:96`: `doc = await _handler.ObtenerDocumentoAsync(docList.First().Id, ct)` — solo el primer documento de la lista | Todos los documentos del workspace se envían a Gemini (o se sintetizan) en una sola operación de análisis |
| Relación entre documentos del mismo workspace | Cada documento se analiza de forma aislada, sin contexto de los demás | El análisis identifica si un documento revoca/deja sin efecto a otro documento anterior del mismo workspace, y lo expone en el resultado |
| Moneda de cada monto extraído | Asumida como USD (`$` sin verificar el texto fuente) | Identificada explícitamente del texto fuente (CLP/USD), o marcada como "no determinada" si el documento no la indica |
| Clasificación de admisibilidad de un oferente | El modelo puede confundir "sin puntaje visible en esta sección" con "declarado inadmisible" | Solo se marca "Inadmisible" cuando el documento lo declara explícitamente así |
| "Monto estimado" | Puede coincidir con el monto ofertado de un participante (TIVIT o competidor) en vez del presupuesto del organismo | Diferenciado explícitamente en el prompt/mapeo: presupuesto del organismo vs. monto ofertado por cada participante |
| Formato de moneda en el dashboard | Inconsistente entre secciones (texto libre del modelo: "DÓLAR AMERICANO" vs "US$") | Normalizado en backend antes de exponerse, un único formateador |
| Badge de estado (ej. "✓ Coherente") | Puede contradecir el texto de la misma sección | Derivado del mismo dato que genera el texto, no de un valor independiente |

## Notificación de análisis completado (frontend, sin persistencia nueva)

| Campo/comportamiento | Estado anterior | Estado corregido |
|---|---|---|
| Seguimiento de transición de estado (`analizando` → `completado`) | `prevEstadoRef`, un `useRef` local a `AnalisisWorkspacePage.tsx` — solo dispara si el usuario está en esa página en el momento exacto del cambio | Movido a un mecanismo a nivel de aplicación (mismo patrón que `NotificationBell.tsx`), sobrevive a la navegación entre páginas |

## Dashboard Ejecutivo (`DashboardEjecutivoDto.AniosDisponibles`)

| Campo | Regla anterior | Regla corregida |
|---|---|---|
| `AniosDisponibles` | `AnalisisService.cs:186`: `todosLosAnios.Add(r.CreadoEn.Year)` — año de creación del registro de análisis | Año real de la licitación (adjudicación si existe, si no publicación) |

## Conversación (Mensajería, `conversaciones`/`conversaciones_participantes`)

| Campo/comportamiento | Estado anterior (con el bug) | Estado corregido |
|---|---|---|
| Selección de participante para conversación Directa | `CrearConversacionModal.tsx:49-52`: `selectValue` (un `useMemo` roto) siempre `undefined`, forzado como `value` del `Select` — la selección real del usuario nunca llega a `participanteIds` | Se elimina el `useMemo`/`value` explícito; `Form.Item` gestiona el valor nativamente (mismo patrón que ya funciona para conversación Grupal) |

No hay cambio de esquema en Mensajería — `usp_Conversaciones_Crear` ya exige exactamente 2 participantes para conversaciones directas; el bug era puramente de frontend, nunca llegaban los datos correctos al backend.
