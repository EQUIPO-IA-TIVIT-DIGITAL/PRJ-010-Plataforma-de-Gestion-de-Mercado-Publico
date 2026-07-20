# Feature Specification: Corrección de hallazgos de code review + QA (Licitaciones / Análisis / Mensajería / Dashboard Ejecutivo / Competidores / Alertas / Scraper v2)

**Feature Branch**: `029-fix-hallazgos-code-review-competidores-alertas`

**Created**: 2026-07-19

**Status**: Draft

**Input**: Dos fuentes combinadas, verificadas ambas directamente contra el código (no se asume ningún hallazgo como correcto sin comprobar):

1. Code review (`/code-review medium`) sobre el diff de la rama actual contra `origin/main` (commit `755c13a` — Buscador Inteligente NL, reconciliación de catálogo, scraper v2, módulo Competidores): 8 hallazgos (5 confirmados, 3 plausibles).
2. `QA/QA-CU010-Reporte-Hallazgos.docx` — reporte de QA (período 2026-07-15 al 2026-07-17, responsable Nilton Fernando Condori Quispe): 20 casos ejecutados, 12 bugs (5 críticos, 5 altos, 2 medios) sobre los módulos Licitaciones, Análisis, Dashboard Ejecutivo y Mensajería.

De los 12 bugs de QA, 2 coinciden con el área ya cubierta por el code review (ambos en `LicitacionHandler.cs`); los otros 10 son hallazgos nuevos sobre módulos que el code review no tocaba (Análisis, Mensajería, Dashboard Ejecutivo, e import histórico masivo de Licitaciones). Esta spec incorpora los 18 hallazgos resultantes (8 del code review + 10 de QA no duplicados) como una sola corrección coordinada antes de que la rama se considere lista para producción.

## Contexto — qué se verificó el 2026-07-19

### Del code review (diff de la rama actual)

- **Reseteo silencioso de estado en cada sync**: `V106__Protect_MergeLicitaciones_rich_data.sql` usa `COALESCE((SELECT codigo FROM estados_licitacion WHERE codigo = EXCLUDED.codigo_estado), 1::SMALLINT)` para `codigo_estado`, a diferencia de `descripcion`/`organismo`/`monto_estimado` que sí usan `COALESCE(licitaciones.x, EXCLUDED.x)` (conservan el valor existente si el nuevo es inválido). El código `1` está explícitamente excluido de los estados válidos por `usp_Catalogos_EstadosLicitacion` (`WHERE e.codigo IN (5,6,7,8,15)`, ver `V108`). Un sync con `codigo_estado` no reconocido pisa un estado válido (ej. Adjudicada) con un código invisible en la UI.
- **Búsqueda natural con fecha de inicio hardcodeada**: `LicitacionHandler.cs`, método `BuscarNaturalAsync`, no tiene parámetro `fechaDesde` y fija `p_fecha_desde` a `DateTime.Parse("2026-01-01")` tanto en la query de items como en la de conteo. El método hermano `ListarAsync` sí soporta un `fechaDesde` arbitrario.
- **Excepción no controlada en análisis de competidores**: `CompetidorGeminiService.cs` indexa `candidates[0]` sin validar que el arreglo no esté vacío. Ni `CompetidorAnalysisService.ObtenerOGenerarAnalisisAsync` ni `CompetidoresController` capturan la excepción, así que una respuesta bloqueada por filtro de seguridad de Gemini se propaga como 500 genérico.
- **Enriquecimiento en caliente sin filtro de borrado lógico**: `AlertasHandler.cs`, método `ActualizarLicitacionEnCalienteAsync`, actualiza `organismo`/`monto_estimado`/`raw_data` filtrando solo por `codigo_externo`, sin el `AND deleted_at IS NULL` que sí tiene la query equivalente en `LicitacionHandler.cs`.
- **Parseo de tabla de ofertas por posición fija**: `cuadroOfertas.js` (`tools/scraper-mp-v2` y `tools/scraper-mp`) destructura las celdas de la tabla de ofertas asumiendo siempre el orden `Rut | Proveedor | Nombre Oferta | Total Oferta | Estado`, validando solo `celdas.length < 4` — no valida el orden contra los headers detectados.
- **(Plausible) Límite de tokens de Gemini reducido en Competidores**: `CompetidorGeminiService.cs` usa `maxOutputTokens = 8192` mientras que `GeminiService.cs` (Análisis) usa `65536`, valor que fue subido específicamente para corregir un bug de truncamiento de respuesta ya documentado en el proyecto. La lógica de armado de request está duplicada en vez de compartida.
- **(Plausible) Monto $0 se muestra como dato faltante**: en `CompetidoresPage.tsx`, `render: (v) => (v ? ... : '—')` trata `0` igual que `null`, ocultando una oferta real de $0.
- **(Plausible) Filtro "solo ofertadas" se pierde en scraper v1 deprecado**: `tools/scraper-mp/modulos/buscar.js` no reaplica el filtro tras el postback de cada estado en el loop de búsqueda (v2 sí lo corrige). Solo es alcanzable si `MpSessionProvider.cs` cae a su fallback hardcodeado hacia `tools/scraper-mp` en vez de `scraper-mp-v2` por falta de configuración de `Extraccion:ExportarSesionScriptPath`.

### De QA — coinciden con el área del code review (verificados contra el código)

- **(QA BUG-002) Filtro de fecha de Licitaciones da 500 y el frontend lo oculta como "sin datos"**: `LicitacionHandler.cs:32-33` (`ListarAsync`, el path de filtros normales usado por `/api/v1/licitaciones`) pasa `p_fecha_desde`/`p_fecha_hasta` dentro de un objeto anónimo sin `DbType.Date` explícito, a diferencia de `BuscarNaturalAsync` (líneas 214/218/228/232) que sí lo especifica. `usp_Licitaciones_Listar` (V093) declara esos parámetros como `DATE`; sin el tipo explícito, Npgsql los envía como "unknown" y Postgres no resuelve el overload de la función (error 42883). El frontend no distingue este 500 de "sin resultados" y muestra la tabla vacía sin ningún aviso.
- **(QA BUG-001, ya resuelto por esta misma rama — verificar como regresión) Filtro "Estado" mostraba entradas duplicadas y algunas no filtraban**: causa raíz reportada por QA: `usp_Catalogos_EstadosLicitacion()` devolvía tanto los códigos legado 1-4 (inventados, nunca usados por datos reales) como los 5 códigos reales. Se verificó `V108__Reconciliar_Catalogo_Tipos_Estados.sql` (ya presente en esta rama): la función ya filtra `WHERE e.codigo IN (5, 6, 7, 8, 15)`, y `LicitacionFilterBar.tsx:25` consume ese catálogo filtrado. Se agrega como caso de regresión a confirmar explícitamente.

### De QA — hallazgos nuevos, fuera del alcance del diff revisado por code review

- **(QA BUG-003, Alto) Import histórico masivo dejó ~99% de las licitaciones con tipo genérico y organismo vacío**: 124.887 de 126.122 licitaciones (cargadas vía `gcloud sql import csv` según `V094__Reset_Licitaciones_Y_Analisis_Para_Import.sql`, no vía `usp_SyncEngine_MergeLicitaciones`) tienen `tipo="Licitacion"` genérico y `organismo` vacío. Los filtros de Tipo "Trato Directo", "Convenio Marco" y "Compra Ágil" no devuelven nada porque ningún registro importado tiene esas etiquetas. Se confirmó además que `LicitacionService.ObtenerPorCodigoAsync` ya contiene un mecanismo de auto-corrección: si `Descripcion` está vacía y `FechaPublicacion` es nula, llama en vivo a `apiMpService.GetDetalleAsync` y sobreescribe `Tipo`/`MontoEstimado` con los valores reales al abrir el detalle — pero esto solo corrige registros que alguien abre individualmente, no el resto.
- **(QA BUG-004, Alto) La notificación "Análisis completado" no es global**: en `AnalisisWorkspacePage.tsx:20,31-51`, el seguimiento de la transición de estado (`prevEstadoRef`) es un `useRef` local a esa página — si el usuario navega a otra página mientras el análisis corre, la notificación nunca aparece.
- **(QA BUG-005, Crítico) "Analizar todo" solo procesa el primer documento del workspace**: causa raíz confirmada en `AnalisisService.cs:91-97` — el caso sin `documentoId` explícito solo toma `docList.First()` y encola un único documento, sin advertir al usuario que el resto del workspace fue ignorado. Produce dashboards incompletos y cifras contradictorias entre corridas.
- **(QA BUG-006, Alto) "Monto estimado" se confunde con el monto ofertado de un participante**: en corridas distintas del mismo workspace, "Monto estimado" coincidió exactamente con el monto ofertado por TIVIT en una corrida, y con el de un competidor (ENTEL) en otra — nunca con un presupuesto independiente del organismo. Indica un problema de mapeo de campos en el prompt de extracción de Gemini, no de "documento equivocado".
- **(QA BUG-007, Medio) Inconsistencias de formato y coherencia interna dentro de un mismo análisis**: mismo hecho con tratamiento visual contradictorio (verde/ascendente en una tarjeta, negativo en otra), formato de moneda inconsistente ("DÓLAR AMERICANO" vs "US$"), tarjetas con nombres casi idénticos pero que miden cosas distintas sin aclararlo, y un badge "✓ Coherente" contradicho por el propio texto de la sección.
- **(QA BUG-008, Crítico) Cifras en pesos chilenos se etiquetan como dólares, inflando montos ~900 veces**: en el análisis de LP-4609, "Monto adjudicado: US\$209.529.081" cuando el documento fuente indica explícitamente que esa cifra es en pesos chilenos. El sistema no identifica la moneda real del texto fuente, asume dólares por defecto.
- **(QA BUG-009, Crítico) Se marcan como "Inadmisible" oferentes que el Informe de Evaluación real declara admisibles**: en el mismo caso LP-4609, el dashboard marca como inadmisibles a Kepler Latam SPA y Tichile Reventa de Software y Hardware SPA, cuando el documento fuente los incluye explícitamente en la lista de ofertas admisibles. Es una conclusión de negocio incorrecta, no un problema de formato.
- **(QA BUG-010, Crítico) El sistema no detecta cuando un documento fue formalmente revocado por otro documento del mismo workspace**: en un caso real, una Resolución Exenta que declaraba a TIVIT inadmisible fue formalmente revocada por una resolución posterior tras un reclamo, con resultado final "No adjudicado" (perdió por criterio técnico) — el dashboard sigue reportando "TIVIT: Inadmisible" como si la resolución revocada siguiera vigente. Directamente relacionado con BUG-005: mientras el sistema analice un documento a la vez sin contexto de precedencia temporal, este riesgo persiste.
- **(QA BUG-011, Medio) El filtro "Todos los años" del Dashboard Ejecutivo usa la fecha de análisis, no la fecha real de la licitación**: `AnalisisService.cs:186`, `todosLosAnios.Add(r.CreadoEn.Year)` usa la fecha de creación del registro de análisis en vez de una fecha real de la licitación (publicación o adjudicación), así que licitaciones de años anteriores analizadas recién en 2026 nunca aparecen bajo su año real.
- **(QA BUG-012, Crítico) No es posible crear una conversación Directa (1 a 1) bajo ninguna circunstancia**: causa raíz confirmada en `CrearConversacionModal.tsx:49-52` — el `useMemo` que calcula `selectValue` retorna `undefined` en ambas ramas (`if (tipo === 'directo') return undefined; return undefined;`), forzando el valor del selector de participantes a `undefined` sin importar la selección real del usuario. El backend rechaza la conversación porque `usp_Conversaciones_Crear` exige exactamente 2 participantes y `participanteIds` llega vacío o incompleto.

## User Scenarios & Testing *(mandatory)*

### User Story 1 - El estado real de una licitación no se pierde al resincronizar (Priority: P1)

Un analista comercial ve el estado correcto (ej. "Adjudicada") de una licitación después de que el sync diario la vuelve a procesar, incluso si el sync trae un código de estado que el sistema no reconoce.

**Why this priority**: Es corrupción de datos silenciosa sobre información ya correcta — el peor tipo de regresión, porque no hay error visible que la delate.

**Independent Test**: Forzar un sync con un `codigo_estado` fuera del catálogo válido sobre una licitación ya marcada "Adjudicada" y confirmar que conserva su estado tras el merge.

**Acceptance Scenarios**:

1. **Given** una licitación con `codigo_estado` válido ya guardado, **When** llega un sync con un `codigo_estado` no reconocido por el catálogo, **Then** la licitación conserva su `codigo_estado` anterior, no se resetea a un valor por defecto.
2. **Given** una licitación nueva sin estado previo, **When** llega un sync con `codigo_estado` no reconocido, **Then** queda marcada de forma distinguible (no como uno de los 5 estados válidos reales) para que se pueda auditar, no silenciosamente como si fuera un estado normal.
3. **Given** el bug de QA (BUG-001: dropdown de "Estado" con entradas duplicadas, algunas sin efecto), **When** se despliega esta rama (con `V108` ya aplicado), **Then** el dropdown muestra cada estado una sola vez y toda selección filtra correctamente — se confirma como regresión ya cerrada, no se reabre como fix nuevo salvo que la verificación falle.

---

### User Story 2 - La búsqueda inteligente encuentra licitaciones de cualquier fecha (Priority: P1)

Un usuario que usa el buscador en lenguaje natural para pedir licitaciones de un período anterior a 2026 obtiene resultados reales, no una lista vacía sin explicación.

**Why this priority**: Rompe una feature recién lanzada (018 - Buscador Inteligente NL) para cualquier consulta que no sea del año en curso, sin ningún mensaje de error que lo delate.

**Independent Test**: Ejecutar una búsqueda NL pidiendo licitaciones de 2025 y confirmar que trae resultados reales existentes en esa fecha.

**Acceptance Scenarios**:

1. **Given** licitaciones existentes con fecha de publicación en 2025, **When** un usuario busca en lenguaje natural algo de ese período, **Then** el sistema las retorna, no una lista vacía.
2. **Given** una consulta NL sin fecha explícita, **When** se ejecuta, **Then** el comportamiento por defecto sigue siendo razonable (no requiere que el usuario especifique fecha para obtener resultados recientes).

---

### User Story 2b - El filtro de fecha normal de Licitaciones funciona en vez de dar 500 (Priority: P1) — QA BUG-002

Un usuario que aplica un filtro de fecha "Desde"/"Hasta" en la lista normal de Licitaciones (no la búsqueda NL) obtiene los resultados dentro de ese rango, en vez de una tabla vacía sin explicación.

**Why this priority**: Bloqueador confirmado por QA y verificado contra el código — el endpoint más usado del módulo (`/api/v1/licitaciones` con filtros) devuelve 500 en cada búsqueda por fecha, y el frontend lo esconde mostrando "sin datos" en vez de un error, así que el usuario ni siquiera sabe que algo falló.

**Independent Test**: Aplicar un filtro de fecha "Desde" o "Hasta" en `/licitaciones` (no el buscador NL) y confirmar que la tabla muestra resultados reales dentro del rango, sin error 500 en la pestaña Network.

**Acceptance Scenarios**:

1. **Given** licitaciones con fecha de publicación conocida, **When** un usuario aplica un filtro "Desde" y/o "Hasta" en la lista normal, **Then** la tabla muestra las licitaciones dentro de ese rango, sin error 500.
2. **Given** un error real e inesperado del backend al filtrar, **When** ocurre, **Then** el frontend lo distingue visualmente de "sin resultados" (mensaje de error visible, no una tabla vacía silenciosa).

---

### User Story 3 - El análisis de un competidor no rompe la página con un error genérico (Priority: P2)

Un usuario que pide el análisis de un competidor cuya respuesta de Gemini fue bloqueada por el filtro de seguridad ve un mensaje de error claro y puede reintentar, no una pantalla rota con un 500 genérico.

**Why this priority**: Afecta disponibilidad del módulo de Competidores (024) ante un caso de borde real de la API de Gemini, pero no corrompe datos.

**Independent Test**: Simular una respuesta de Gemini con `candidates` vacío y confirmar que el usuario recibe un mensaje de error manejado, no un crash.

**Acceptance Scenarios**:

1. **Given** Gemini retorna una respuesta sin `candidates` (bloqueo de seguridad), **When** se solicita el análisis de un competidor, **Then** el usuario ve un mensaje de error claro en vez de un error 500 sin contexto.
2. **Given** ese mismo caso, **When** ocurre, **Then** no se guarda un análisis parcial o corrupto en caché — el usuario puede reintentar limpiamente.

---

### User Story 4 - Los datos de licitaciones eliminadas no se resucitan (Priority: P2)

Una licitación marcada como eliminada (borrado lógico, por deduplicación o reconciliación de catálogo) permanece eliminada aunque una alerta en curso intente enriquecerla con datos frescos del portal.

**Why this priority**: Rompe la garantía de borrado lógico existente en el resto del sistema (`deleted_at`), generando inconsistencia entre módulos.

**Independent Test**: Marcar una licitación como eliminada, disparar el flujo de enriquecimiento en caliente de Alertas sobre ella, y confirmar que no se actualiza.

**Acceptance Scenarios**:

1. **Given** una licitación con `deleted_at` no nulo, **When** el enriquecimiento en caliente de Alertas intenta actualizarla, **Then** la actualización no se aplica.

---

### User Story 5 - El monto y estado de una oferta se registran correctamente aunque cambie el layout de la tabla (Priority: P3)

Un analista de Competidores ve montos y estados de ofertas correctos, incluso si el portal de Mercado Público presenta la tabla de "Cuadro de Ofertas" con columnas en un orden distinto al habitual.

**Why this priority**: Riesgo de corrupción silenciosa de datos de ofertas, pero requiere una variante de layout del portal que aún no se ha confirmado que ocurra en producción — prioridad menor a los casos ya confirmados con datos reales.

**Independent Test**: Simular una fila de tabla con columnas en orden distinto al esperado y confirmar que el scraper detecta la discrepancia en vez de asignar valores a las columnas equivocadas.

**Acceptance Scenarios**:

1. **Given** una tabla de ofertas cuyo orden de columnas no coincide con el orden esperado, **When** el scraper la procesa, **Then** detecta la discrepancia (vía validación contra los headers) y no guarda monto/estado en la columna equivocada.

---

### User Story 6 - Las licitaciones del import histórico muestran su tipo y organismo real (Priority: P2) — QA BUG-003

Un usuario que filtra por Tipo (Trato Directo, Convenio Marco, Compra Ágil) o revisa el organismo de una licitación ve el dato real, sin importar si esa licitación llegó por el scraper o por el import histórico masivo.

**Why this priority**: Afecta ~99% de la base histórica (124.887 de 126.122 licitaciones) y rompe 3 de los 4 valores del filtro de Tipo — es deuda de datos masiva, pero no bloquea el uso del sistema para licitaciones nuevas (que sí llegan bien tipadas vía scraper/sync).

**Independent Test**: Aplicar el filtro Tipo = "Trato Directo" (o "Convenio Marco"/"Compra Ágil") y confirmar que devuelve resultados reales en vez de una lista vacía.

**Acceptance Scenarios**:

1. **Given** una licitación cargada por el import histórico masivo con tipo real "Trato Directo", **When** un usuario filtra por ese tipo, **Then** la licitación aparece en los resultados.
2. **Given** una licitación del import masivo sin organismo asignado, **When** el backfill se ejecuta, **Then** queda con su organismo real si es recuperable desde la API de Mercado Público.
3. **Given** una licitación ya auto-corregida al abrir su detalle (mecanismo existente en `ObtenerPorCodigoAsync`), **When** se ejecuta el backfill masivo, **Then** no se sobreescribe con un valor peor ni se duplica el trabajo ya hecho.

---

### User Story 7 - "Analizar todo" analiza realmente todos los documentos del workspace (Priority: P1) — QA BUG-005

Un usuario que sube varios documentos de una misma licitación (Resolución de Adjudicación, Informe de Evaluación, DJ, etc.) y hace clic en "Analizar todo" obtiene un dashboard que sintetiza información de todos ellos, no solo del primero.

**Why this priority**: Es el hallazgo más grave del módulo núcleo del sistema (Análisis) — genera dashboards incompletos y cifras contradictorias entre corridas, y es la causa raíz que habilita BUG-006, BUG-008, BUG-009 y BUG-010 (todos dependen de qué documento(s) realmente se analizaron).

**Independent Test**: Crear un workspace con 4+ documentos de una misma licitación, usar "Analizar todo", y confirmar en el dashboard que la información proviene de más de un documento (ej. cruzando un dato que solo aparece en el segundo o tercer documento).

**Acceptance Scenarios**:

1. **Given** un workspace con múltiples documentos, **When** el usuario usa "Analizar todo", **Then** el dashboard resultante sintetiza información de todos los documentos subidos, no solo del primero de la lista.
2. **Given** que la síntesis multi-documento real no esté disponible en una iteración temprana, **When** el sistema solo analiza el documento más reciente, **Then** el botón lo comunica honestamente (ej. "Analizar documento más reciente"), no como si cubriera todo el workspace.

---

### User Story 8 - Una conclusión revocada no se presenta como vigente (Priority: P1) — QA BUG-010

Un usuario que analiza un documento que fue formalmente revocado por otro documento posterior del mismo workspace ve una advertencia de que existe una versión más reciente, no la conclusión revocada presentada con total confianza.

**Why this priority**: Es el hallazgo más delicado desde una perspectiva legal/de negocio — una conclusión "TIVIT fue descalificado" que ya no es cierta podría llevar a decisiones internas equivocadas. Depende de US7 (análisis multi-documento) para resolverse de raíz.

**Independent Test**: Subir dos versiones de una resolución donde la segunda revoca a la primera, analizar específicamente el documento revocado, y confirmar que el sistema advierte sobre la existencia de la versión posterior.

**Acceptance Scenarios**:

1. **Given** un workspace con un documento que revoca explícitamente a otro documento anterior del mismo workspace, **When** se analiza el documento revocado, **Then** el sistema advierte que existe un documento posterior que lo deja sin efecto.

---

### User Story 9 - Los montos del análisis se muestran en la moneda real del documento fuente (Priority: P1) — QA BUG-008

Un usuario que revisa el "Monto adjudicado" u otro monto del dashboard ve la moneda correcta (CLP o USD según lo que el documento realmente indica), no un símbolo de dólar aplicado por defecto a una cifra en pesos chilenos.

**Why this priority**: Es el hallazgo de mayor severidad financiera del módulo — un error de magnitud (no de formato) que puede inflar un monto ~900 veces y llevar a decisiones de negocio basadas en cifras equivocadas por órdenes de magnitud.

**Independent Test**: Analizar el Informe de Evaluación de LP-4609 (o un documento equivalente con una cifra en CLP explícita) y confirmar que "Monto adjudicado" se muestra en pesos chilenos, no en dólares.

**Acceptance Scenarios**:

1. **Given** un documento fuente que indica explícitamente que una cifra está en pesos chilenos, **When** se analiza, **Then** el dashboard muestra esa cifra en pesos chilenos, no con símbolo de dólar.
2. **Given** un documento fuente que sí reporta una cifra en dólares, **When** se analiza, **Then** el dashboard la muestra correctamente en dólares — la corrección no debe invertir el problema.

---

### User Story 10 - Solo se marca "Inadmisible" a quien el documento real declara así (Priority: P1) — QA BUG-009

Un usuario que revisa la tabla de "Ofertantes" del dashboard ve exactamente las mismas empresas marcadas como inadmisibles que el Informe de Evaluación real, ni una más.

**Why this priority**: Es una conclusión de negocio incorrecta (no de formato) que puede llevar a TIVIT a sacar conclusiones equivocadas sobre por qué compitió contra menos empresas de las reales.

**Independent Test**: Analizar el Informe de Evaluación de LP-4609 y confirmar que Kepler Latam SPA y Tichile Reventa de Software y Hardware SPA aparecen como admisibles (no inadmisibles), igual que en el documento fuente.

**Acceptance Scenarios**:

1. **Given** un Informe de Evaluación que declara admisible a un oferente, **When** se analiza, **Then** el dashboard no lo marca como "Inadmisible".
2. **Given** un oferente que el documento sí declara inadmisible, **When** se analiza, **Then** el dashboard lo sigue marcando correctamente como inadmisible — la corrección no debe volverse permisiva en el otro sentido.

---

### User Story 11 - "Monto estimado" no se confunde con el monto ofertado de un participante (Priority: P2) — QA BUG-006

Un usuario que revisa "Monto estimado" en el dashboard ve el presupuesto que el organismo fijó antes de recibir ofertas, no el monto que ofertó TIVIT o un competidor.

**Why this priority**: Es un error de mapeo de campos que afecta la confiabilidad de una métrica clave del dashboard, pero no es tan grave como una clasificación legal incorrecta (US8, US10) — el dato erróneo es más fácil de detectar por contraste manual.

**Independent Test**: Analizar un workspace y confirmar que "Monto estimado" no coincide exactamente con ningún "Monto ofertado" individual (TIVIT o competidores), salvo coincidencia real documentada.

**Acceptance Scenarios**:

1. **Given** un documento que reporta tanto el presupuesto del organismo como los montos ofertados por los participantes, **When** se analiza, **Then** "Monto estimado" refleja el presupuesto del organismo, no el monto de ningún ofertante específico.

---

### User Story 12 - La notificación de análisis completado llega sin importar en qué página esté el usuario (Priority: P2) — QA BUG-004

Un usuario que inicia un análisis y navega a otra parte de la aplicación recibe la notificación de "Análisis completado" cuando termina, tal como el mensaje de inicio lo promete.

**Why this priority**: Rompe una promesa explícita de la propia UI ("puedes seguir navegando"), pero es un problema de UX/alcance de notificación, no de integridad de datos.

**Independent Test**: Iniciar un análisis, navegar a otra página, esperar a que termine sin volver al workspace, y confirmar que la notificación aparece igual.

**Acceptance Scenarios**:

1. **Given** un análisis en curso, **When** el usuario navega fuera de la página del workspace antes de que termine, **Then** recibe la notificación de completado sin importar en qué parte de la app esté.

---

### User Story 13 - El dashboard de análisis usa un formato y una señalización consistentes (Priority: P3) — QA BUG-007

Un usuario que revisa un dashboard de análisis ve el mismo hecho representado de forma coherente en todas sus secciones (mismo formato de moneda, señalización de color acorde al signo real del dato, métricas con nombres claramente diferenciados).

**Why this priority**: Afecta confianza y claridad, pero no cambia ninguna conclusión de negocio ni corrompe datos — es el hallazgo de menor severidad del módulo de Análisis.

**Independent Test**: Generar un análisis y revisar el dashboard completo, confirmando que la misma diferencia numérica no aparece con signo/color contradictorio en dos tarjetas distintas.

**Acceptance Scenarios**:

1. **Given** un dashboard generado, **When** se revisa de principio a fin, **Then** el formato de moneda es consistente en todo el render (no mezcla "DÓLAR AMERICANO" con "US$" para el mismo tipo de dato).
2. **Given** una métrica marcada con un badge de estado (ej. "✓ Coherente"), **When** el texto de la misma sección contradice ese badge, **Then** el badge refleja el estado real, no un valor por defecto desconectado del contenido.

---

### User Story 14 - El filtro de año del Dashboard Ejecutivo usa la fecha real de la licitación (Priority: P3) — QA BUG-011

Un usuario que filtra el Dashboard Ejecutivo por año puede elegir cualquier año en que haya ocurrido realmente una licitación analizada, no solo el año en que se ejecutó el análisis en la plataforma.

**Why this priority**: Limita la utilidad de comparación histórica del dashboard, pero no es un bloqueador ni afecta la exactitud de los datos ya mostrados para el año actual.

**Independent Test**: Con licitaciones analizadas cuya fecha real es de un año anterior a 2026, abrir el filtro de año y confirmar que ese año anterior aparece como opción.

**Acceptance Scenarios**:

1. **Given** licitaciones analizadas cuya fecha real (publicación o adjudicación) es de 2024 o 2025, **When** se abre el filtro de año del Dashboard Ejecutivo, **Then** esos años aparecen como opciones seleccionables.

---

### User Story 15 - Es posible crear una conversación directa (1 a 1) (Priority: P1) — QA BUG-012

Un usuario que crea una nueva conversación de tipo "Directa (1 a 1)", selecciona un participante y confirma, ve la conversación creada exitosamente.

**Why this priority**: Bloqueador total y confirmado — el tipo de conversación más básico de Mensajería no funciona bajo ninguna circunstancia, con causa raíz ya identificada en el código.

**Independent Test**: Crear una conversación directa con un participante real y confirmar que se crea exitosamente y aparece en la lista de conversaciones.

**Acceptance Scenarios**:

1. **Given** el modal de nueva conversación con Tipo = "Directa (1 a 1)", **When** el usuario selecciona un participante y confirma, **Then** la conversación se crea con exactamente ese participante y el usuario actual.
2. **Given** una conversación grupal (que ya funciona), **When** se prueba tras esta corrección, **Then** sigue funcionando sin regresión.

### Edge Cases

- ¿Qué pasa si el catálogo de estados (`estados_licitacion`) legítimamente agrega un código nuevo que el sistema aún no conoce? La corrección de US1 no debe bloquear la incorporación de estados nuevos vía migración — solo debe evitar el reseteo silencioso a un código inválido cuando el dato entrante no matchea el catálogo actual.
- ¿Qué pasa si un usuario de la búsqueda NL sí quiere acotar explícitamente a "desde 2026"? La corrección de US2 debe seguir permitiendo acotar por fecha cuando el usuario lo pide, solo elimina el hardcodeo que bloquea todo lo anterior.
- ¿Qué pasa si Gemini bloquea la respuesta repetidamente para el mismo competidor (contenido consistentemente filtrado)? El sistema debe permitir reintentos sin degradar la experiencia, pero no está en alcance resolver por qué Gemini bloquea el contenido.
- ¿Qué pasa si un usuario aplica el filtro de fecha normal junto con otros filtros (estado, tipo, organismo) a la vez? La corrección de US2b debe funcionar en combinación con cualquier otro filtro ya soportado por `usp_Licitaciones_Listar`, no solo con la fecha aislada.
- ¿Qué pasa si la verificación de regresión de US1 (QA BUG-001) efectivamente falla en el entorno de QA? En ese caso deja de ser una simple verificación y se trata como un bug nuevo con la misma prioridad que tenía en el reporte de QA (Alto), no se asume resuelto solo porque el código de esta rama sugiere que lo está.
- ¿Qué pasa si el organismo real de una licitación del import masivo (US6) no es recuperable ni siquiera desde la API de Mercado Público (ej. licitación ya no disponible en el portal)? Debe quedar explícitamente marcada como "no recuperable" en vez de reintentar indefinidamente o quedar en el mismo estado ambiguo de hoy.
- ¿Qué pasa si "Analizar todo" (US7) se ejecuta sobre un workspace con un único documento? Debe comportarse igual que hoy (analiza ese único documento), la corrección solo cambia el caso de 2+ documentos.
- ¿Qué pasa si dos documentos del mismo workspace se contradicen sin que uno revoque formalmente al otro (a diferencia del caso de US8, donde sí hay revocación explícita)? Fuera de alcance de US8 — esta spec solo exige detectar revocación formal explícita en el texto, no inferir contradicciones no declaradas.
- ¿Qué pasa si un documento fuente no indica ninguna moneda explícita para una cifra (US9)? El sistema no debe asumir dólares por defecto silenciosamente — debe quedar marcado como moneda no determinada en vez de adivinar.
- ¿Qué pasa si un usuario intenta crear una conversación directa con un participante que ya tiene una conversación directa existente con el usuario actual (US15)? Fuera de alcance definir ese comportamiento en esta spec — se corrige el bloqueador de creación; el comportamiento ante duplicados se resuelve con la lógica ya existente de `usp_Conversaciones_Crear`, si la tiene.

## Requirements *(mandatory)*

### Functional Requirements

- **FR-001**: El sistema DEBE conservar el `codigo_estado` existente de una licitación cuando un sync entrante trae un valor no reconocido por el catálogo de estados, en vez de reemplazarlo por un valor por defecto.
- **FR-002**: El sistema DEBE permitir que la búsqueda en lenguaje natural de licitaciones acepte cualquier rango de fechas real de los datos, no solo desde el 1 de enero de 2026.
- **FR-003**: El sistema DEBE capturar y manejar de forma controlada cualquier error al parsear una respuesta de Gemini sin candidatos válidos en el flujo de análisis de competidores, devolviendo un error claro al usuario en vez de un 500 no controlado.
- **FR-004**: El sistema NO DEBE modificar datos de una licitación con borrado lógico activo (`deleted_at` no nulo) a través del flujo de enriquecimiento en caliente de Alertas.
- **FR-005**: El sistema DEBE validar que el orden de columnas detectado en la tabla de "Cuadro de Ofertas" coincide con el orden esperado antes de asignar monto y estado, marcando como no reconocida cualquier fila que no coincida en vez de asignar valores a la columna incorrecta.
- **FR-006**: El sistema DEBE unificar la lógica de armado y parseo de solicitudes a Gemini entre `GeminiService` (Análisis) y `CompetidorGeminiService` (Competidores) en un componente compartido, para que ajustes futuros (como el límite de tokens de salida) apliquen a ambos módulos sin duplicación.
- **FR-007**: El sistema DEBE distinguir visualmente un monto de oferta de $0 (dato real) de un monto ausente (sin dato) en la vista de Competidores.
- **FR-008**: El sistema DEBE aplicar el filtro "solo licitaciones con oferta" de forma consistente en cada búsqueda por estado del scraper, sin perderlo entre postbacks consecutivos de la misma sesión.
- **FR-009** (QA BUG-002): El sistema DEBE aceptar un filtro de fecha "Desde"/"Hasta" en la lista normal de Licitaciones (`ListarAsync`/`usp_Licitaciones_Listar`) sin producir un error 500, tipando explícitamente los parámetros de fecha al llamar al stored procedure. El frontend DEBE mostrar un error visible si la petición realmente falla, en vez de presentarlo como "sin resultados".
- **FR-010** (QA BUG-003): El sistema DEBE re-derivar el tipo real de las licitaciones del import histórico masivo a partir de una fuente confiable (sufijo del código externo y/o la API real de Mercado Público), y DEBE ejecutar el mecanismo de enriquecimiento ya existente (`ObtenerPorCodigoAsync` → `apiMpService.GetDetalleAsync` → `ActualizarDetalleAsync`) como un job de backfill masivo sobre los registros afectados, en vez de depender solo de que cada uno se abra individualmente.
- **FR-011** (QA BUG-005): El sistema DEBE, al usar "Analizar todo", procesar todos los documentos del workspace (no solo el primero), sintetizando o consolidando su información en un único resultado. Si esta capacidad no está disponible aún en una iteración dada, el botón DEBE comunicarlo honestamente en su etiqueta.
- **FR-012** (QA BUG-010): El sistema DEBE detectar cuándo un documento del workspace revoca formalmente a otro documento anterior del mismo workspace, y DEBE advertir explícitamente sobre esa revocación en vez de presentar la conclusión revocada como vigente.
- **FR-013** (QA BUG-008): El sistema DEBE identificar la moneda real (CLP/USD) de cada cifra extraída de un documento fuente a partir del texto explícito, y DEBE mostrarla en el dashboard con esa moneda real, sin asumir dólares por defecto.
- **FR-014** (QA BUG-009): El sistema DEBE clasificar como "Inadmisible" únicamente a los oferentes que el documento fuente declara explícitamente así, sin confundir "sin puntaje visible en esta sección" con "declarado inadmisible".
- **FR-015** (QA BUG-006): El sistema DEBE distinguir explícitamente, en el prompt de extracción y en el mapeo de resultado, el "monto estimado/presupuesto del organismo" del "monto ofertado por cada participante", de forma que nunca queden confundidos.
- **FR-016** (QA BUG-004): El sistema DEBE notificar la finalización de un análisis sin importar en qué página de la aplicación se encuentre el usuario en ese momento.
- **FR-017** (QA BUG-007): El sistema DEBE normalizar el formato de moneda de un análisis en el backend (no depender del texto libre del modelo), y DEBE usar nombres de métrica no ambiguos entre sí cuando midan conceptos distintos.
- **FR-018** (QA BUG-011): El sistema DEBE construir el filtro de año del Dashboard Ejecutivo a partir de una fecha real de la licitación (publicación o adjudicación), no de la fecha de creación del registro de análisis.
- **FR-019** (QA BUG-012): El sistema DEBE permitir crear una conversación Directa (1 a 1) seleccionando un participante real, sin que el valor del selector quede forzado a `undefined`.

### Key Entities

- **Licitación**: registro cuyo `codigo_estado` no debe perderse ante datos entrantes inválidos, cuyo borrado lógico (`deleted_at`) debe respetarse en todos los flujos de escritura, y cuyo tipo/organismo debe reflejar el dato real sin importar si llegó por scraper, sync o import histórico masivo. Su filtrado por fecha (búsqueda normal y natural) debe devolver resultados reales, no un error oculto ni una fecha de inicio hardcodeada.
- **Consulta de búsqueda natural**: interpretación de una consulta en lenguaje natural que debe soportar cualquier rango de fechas real, no uno hardcodeado.
- **Análisis de competidor**: resultado generado por Gemini a partir de las ofertas de un competidor; debe fallar de forma controlada y no quedar duplicado con la lógica de Análisis de documentos.
- **Oferta**: fila de la tabla "Cuadro de Ofertas" de un competidor; su monto y estado deben quedar correctamente asociados a las columnas reales del portal, con $0 tratado como dato válido.
- **Catálogo de estados de licitación**: fuente única para el filtro "Estado" del frontend; debe exponer solo los 5 códigos vigentes, no los códigos legado 1-4 (ya resuelto por `V108`, cubierto aquí como regresión a confirmar).
- **Workspace de análisis**: conjunto de documentos de una licitación; "Analizar todo" debe procesar todos sus documentos, y el sistema debe reconocer relaciones de precedencia/revocación entre ellos en vez de tratarlos como hechos independientes y no relacionados.
- **Dashboard de análisis**: resultado presentado al usuario; sus montos deben llevar la moneda real del documento fuente, su clasificación de admisibilidad debe coincidir con el documento real, su "monto estimado" no debe confundirse con un monto ofertado, y su formato/señalización debe ser interno-consistente.
- **Notificación de análisis completado**: debe alcanzar al usuario sin importar la página activa, no solo mientras permanece en el workspace.
- **Dashboard Ejecutivo**: su filtro de año debe reflejar años reales de licitaciones analizadas, no años de ejecución del análisis.
- **Conversación (Mensajería)**: una conversación de tipo Directa debe poder crearse con exactamente 2 participantes reales, igual que ya funciona para el tipo Grupal.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: Cero licitaciones cambian su `codigo_estado` a un valor fuera del catálogo válido (5,6,7,8,15) como resultado de un sync.
- **SC-002**: Una búsqueda en lenguaje natural sobre licitaciones de 2025 retorna resultados reales existentes en ese período.
- **SC-003**: Una respuesta de Gemini sin candidatos válidos produce un mensaje de error manejado en el análisis de competidores, cero errores 500 no controlados por este caso.
- **SC-004**: Cero licitaciones con `deleted_at` no nulo reciben actualizaciones vía el enriquecimiento en caliente de Alertas.
- **SC-005**: Cero filas de la tabla de ofertas con orden de columnas distinto al esperado quedan guardadas con monto/estado en la columna incorrecta.
- **SC-006**: Un monto de oferta de $0 se distingue visualmente de un monto ausente en el 100% de los casos en la vista de Competidores.
- **SC-007** (QA BUG-002): Una petición a `/api/v1/licitaciones` con `fechaDesde` y/o `fechaHasta` retorna 200 con resultados reales, cero errores 500 causados por tipeo de parámetro de fecha.
- **SC-008** (QA BUG-001, regresión): El dropdown de filtro "Estado" en `/licitaciones` muestra exactamente 5 opciones (una por cada código vigente), sin duplicados, y el 100% de las selecciones filtra la tabla correctamente.
- **SC-009** (QA BUG-003): El filtro Tipo = "Trato Directo"/"Convenio Marco"/"Compra Ágil" devuelve resultados reales sobre licitaciones del import histórico masivo, no listas vacías.
- **SC-010** (QA BUG-005): Un workspace con 4+ documentos analizado con "Analizar todo" produce un dashboard cuya información proviene verificablemente de más de un documento.
- **SC-011** (QA BUG-010): Un documento formalmente revocado por otro documento del mismo workspace, al ser analizado, muestra una advertencia de revocación en el dashboard.
- **SC-012** (QA BUG-008): Cero montos etiquetados con una moneda distinta a la que el documento fuente indica explícitamente, en una muestra de re-análisis de los casos ya identificados por QA.
- **SC-013** (QA BUG-009): Cero oferentes marcados como "Inadmisible" que el documento fuente declara admisibles, en el caso LP-4609 re-analizado tras el fix.
- **SC-014** (QA BUG-006): "Monto estimado" no coincide exactamente con ningún "Monto ofertado" individual salvo que el documento fuente confirme que son, en efecto, el mismo valor.
- **SC-015** (QA BUG-004): La notificación de "Análisis completado" aparece en el 100% de los casos de prueba en los que el usuario no permanece en la página del workspace durante la transición.
- **SC-016** (QA BUG-007): Un dashboard generado tras el fix no presenta el mismo hecho numérico con señalización de color contradictoria en dos secciones distintas, en una revisión manual de los 4 ejemplos documentados por QA.
- **SC-017** (QA BUG-011): El filtro de año del Dashboard Ejecutivo incluye al menos un año anterior a 2026 cuando existen licitaciones analizadas con fecha real de ese año.
- **SC-018** (QA BUG-012): Una conversación Directa (1 a 1) se crea exitosamente en el 100% de los intentos con un participante válido seleccionado.

## Assumptions

- La corrección de FR-001 no requiere reprocesar licitaciones ya afectadas retroactivamente salvo que, al implementar, se detecten casos reales con estado ya corrompido a código `1` — en ese caso se corrige con el mismo mecanismo usado en la spec `028-fix-estado-tipo-scraper-tivit`, sin requerir spec nueva.
- FR-006 (unificar cliente de Gemini) es la forma correcta de resolver también el hallazgo plausible de `maxOutputTokens` reducido en Competidores — al compartir el componente, ambos módulos usan el mismo límite validado.
- El hallazgo del scraper v1 (filtro perdido en postbacks) no requiere corrección de código porque v1 está deprecado (`tools/scraper-mp/DEPRECATED.md`) y v2 ya lo resuelve — el alcance de esta spec es asegurar que `MpSessionProvider.cs` nunca caiga al fallback hacia v1 sin que quede explícito en configuración, para que ese código muerto no vuelva a ejecutarse por accidente.
- FR-009 (QA BUG-002) se corrige con el mismo patrón que `ActualizarDetalleAsync` y `BuscarNaturalAsync` ya usan en el mismo archivo (`DbType.Date`/`DbType.DateTime2` explícito) — no requiere cambiar la firma del stored procedure `usp_Licitaciones_Listar`, que ya declara los parámetros correctamente como `DATE`.
- SC-008 (QA BUG-001) se valida como regresión, no como desarrollo nuevo — si la verificación en `quickstart.md` falla, se reclasifica como bug abierto con la misma prioridad (Alto) que tenía en el reporte de QA, no se degrada silenciosamente.
- FR-010 (QA BUG-003) asume que el sufijo del `codigo_externo` (patrón observado: LE/LP/LQ/LR/etc.) es suficiente para re-derivar el tipo sin re-scrapear; el organismo puede requerir la llamada en vivo a la API de Mercado Público (mecanismo ya existente) si no es recuperable del CSV original. Si un registro no es recuperable por ningún medio, queda marcado explícitamente como tal (ver Edge Cases), no en el mismo estado ambiguo de hoy.
- FR-011/FR-012 (QA BUG-005 y BUG-010) se abordan juntos porque comparten la misma causa raíz de fondo (el sistema analiza un documento sin contexto de los demás documentos del workspace) — la implementación exacta (síntesis multi-documento real vs. detección de precedencia/revocación como paso intermedio) se decide en la fase de research según el esfuerzo real de cada enfoque.
- FR-013/FR-014/FR-015 (QA BUG-008, BUG-009, BUG-006) son ajustes al prompt de extracción de Gemini y/o al post-procesamiento del resultado en `AnalisisService`/`GeminiService` — no requieren cambio de esquema de base de datos, solo de la lógica de extracción y mapeo ya existente.
- Los 10 hallazgos de QA fuera del área original del code review (BUG-003 a BUG-012) se priorizan según la severidad que QA ya les asignó (5 críticos: BUG-005, BUG-008, BUG-009, BUG-010, BUG-012; el resto Alto o Medio), no se re-priorizan arbitrariamente en esta spec.
