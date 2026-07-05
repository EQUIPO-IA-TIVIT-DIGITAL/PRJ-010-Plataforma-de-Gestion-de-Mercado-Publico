# Research: Ajustes Urgentes del Cliente

**Feature**: 017-ajustes-urgentes-cliente | **Date**: 2026-07-01

Decisiones técnicas que resuelven los puntos abiertos del Technical Context. No quedan NEEDS CLARIFICATION.

## R1 — Manejo centralizado de sesión expirada (401)

**Decision**: Crear `src/mpm-web/src/lib/apiClient.ts`: un wrapper de `fetch` que (a) adjunta el token de `localStorage['mpm_token']`, (b) ante respuesta 401 dispara un handler global de sesión expirada, y (c) lanza errores tipados para el resto. El handler (`sessionExpired()` en `useAuth`) usa un flag módulo-level para ejecutarse **una sola vez** aunque N requests fallen en paralelo: limpia `mpm_token`/`mpm_user`, detiene la conexión SignalR si existe, guarda un flag `mpm_session_expired` y hace `window.location.replace('/login')`. `LoginPage` lee ese flag y muestra el aviso "Tu sesión expiró, inicia sesión nuevamente". Todos los hooks (`useLicitaciones`, `useNotificaciones`, `useAnalisis`, etc.) migran su `fetch` crudo al wrapper.

**Rationale**: Hoy cada hook hace `fetch` propio y en 401 solo lanza `Error`, dejando la app llena de mensajes rotos. No hay axios en el proyecto y no se justifica agregarlo: un wrapper de ~40 líneas resuelve el problema manteniendo el stack actual. `window.location.replace` (vs. navegación de router) garantiza el desmonte total del árbol y evita estados residuales (FR-003).

**Alternatives considered**: (1) Agregar axios con interceptores — dependencia nueva innecesaria; (2) manejar 401 en cada hook — repite el bug actual, propenso a olvidos; (3) validar expiración del JWT con un timer local decodificando `exp` — complementario pero insuficiente (no cubre revocación server-side); se descarta para este lote por simplicidad.

## R2 — Exportación PDF estructurada

**Decision**: Reemplazar la captura `html2canvas` → imagen por generación programática client-side con `jspdf` + **`jspdf-autotable`** (nueva dependencia liviana, misma familia que la actual). Nuevo módulo `src/mpm-web/src/lib/analisisPdf.ts` que recibe el objeto de resultado del análisis (ya disponible en el dashboard) y compone: portada/encabezado, resumen ejecutivo, comparativa de puntajes, fortalezas/debilidades, motivo del resultado, **comparativa de documentos** y recomendaciones — con texto real, saltos de página controlados y tablas con `autotable`.

**Rationale**: El requerimiento exige texto seleccionable y paginación correcta (FR-016). `jspdf` ya está en el proyecto; `autotable` resuelve tablas multipágina sin cortes ilegibles. Generar desde el objeto de datos (no desde el DOM) hace el PDF independiente del layout de pantalla.

**Alternatives considered**: (1) Server-side con QuestPDF — mejor tipografía pero agrega dependencia backend, endpoint nuevo y trabajo que no cabe para mañana; (2) `@react-pdf/renderer` — bundle pesado y duplicación de componentes; (3) `pdfmake` — viable pero reemplaza en vez de extender el stack actual.

## R3 — Validación cruzada documentos enviados vs. veredicto (coherencia del análisis)

**Decision**: Dos capas:
1. **Prompt** (`GeminiService.GetAnalisisPrompt()`): agregar al esquema JSON de salida una sección `validacion_documental` con `documentos: [{nombre, requerido, enviado, observado_en_acta, estado}]` e `inconsistencias: [{documento, dice_acta, evidencia, severidad}]`. El prompt instruye explícitamente: "contrasta los documentos adjuntos entregados contra los antecedentes requeridos y contra lo que el acta declara como faltante u observado; si el acta afirma que un documento faltó pero está entre los adjuntos, decláralo como inconsistencia — no repitas el motivo del acta sin verificarlo".
2. **Post-proceso** (`AnalisisService`): verificación determinística al recibir el JSON — cruza la lista de archivos del workspace (documentos que subimos/enviamos, ya conocidos por el módulo) contra `analisis_perdida.motivo_principal` y `validacion_documental`; si el motivo menciona un documento que existe entre los enviados y Gemini no lo marcó, se agrega la inconsistencia por código. El resultado viaja dentro del JSON de análisis existente (columna actual), **sin tablas nuevas**.

**Rationale**: El prompt ya extrae `requisitos.antecedentes_requeridos` y `documentos_adjuntos`, así que la materia prima existe; la capa determinística protege contra el punto débil conocido de los LLM (repetir el acta sin cuestionarla). Guardar dentro del JSON evita migraciones y respeta el principio III.

**Alternatives considered**: (1) Solo prompt — insuficiente, el caso reportado por el cliente ("la tesis dice que perdimos por no mandar un documento pero sí lo mandamos") es exactamente el que el LLM ya falla; (2) tabla nueva `analisis_validaciones` — persistencia extra sin consumidor que la requiera hoy.

## R4 — Sincronización semanal 2025-2026

**Decision**: `SyncEngineService`: cadencia del timer configurable `Sync:IntervalDays` (default **7**), ventana incremental de **8 días** (1 día de solapamiento anti-huecos). Backfill one-shot: al arrancar, si la marca `sync_backfill_2025` no existe en el log de sync, ejecuta una pasada histórica desde 01-01-2025 hasta hoy (reutilizando el recorrido día-a-día existente) y registra la marca. Fallos: ya existe log de sync (`usp_SyncLog_*`); se garantiza que toda excepción del ciclo quede registrada y el timer siga vivo para el siguiente ciclo (FR-013).

**Rationale**: Cumple "datos 2025-2026 actualizados al menos cada semana" (FR-012, SC-005) reutilizando el motor actual; el solapamiento de 1 día evita perder licitaciones publicadas en el borde de la ventana. Al quitar el botón "Sincronizar" del frontend, el endpoint POST `/sync` se conserva (útil para operación/emergencias) pero sin UI.

**Alternatives considered**: (1) Mantener sync diario — cumple de sobra la SC pero el cliente pidió explícitamente semanal; se implementa configurable para poder ajustar; (2) job externo (cron/Cloud Scheduler) — infraestructura nueva innecesaria, el hosted service existente basta.

## R5 — Borrar notificaciones

**Decision**: Migración `V075__Notificaciones_Eliminar_SPs.sql` con `usp_Notificaciones_Eliminar(p_id, p_user_id, p_tenant_id)` (borra solo si pertenece al usuario/tenant) y `usp_Notificaciones_EliminarTodas(p_user_id, p_tenant_id)`. Endpoints `DELETE /api/v1/notificaciones/{id}` y `DELETE /api/v1/notificaciones`. Frontend: icono eliminar por fila + botón "Borrar todas" con confirmación (`Popconfirm`), mutaciones TanStack Query que invalidan lista y contador de no leídas.

**Rationale**: Sigue exactamente el patrón existente del módulo (SPs + Dapper + TenantContext). Borrado físico (sin papelera) según supuesto del spec.

**Alternatives considered**: Soft-delete con columna `eliminada` — sin requisito de restauración, complejidad innecesaria.

## R6 — Chat contextual en vista propia + validación de formato

**Decision**: Extraer el bloque de chat del dashboard a componente `AnalisisChat.tsx` reutilizable; nueva ruta `/analisis/:id/chat` con `AnalisisChatPage.tsx` (layout de página completa, historial más alto, mismo hook/endpoints). El dashboard conserva un acceso ("Abrir chat en vista completa"). Formato: (a) el system prompt del chat (`GeminiService`, línea ~132) se endurece: "responde siempre en Markdown válido, sin fences ```json, con listas y tablas Markdown"; (b) en frontend, normalizador previo a `react-markdown` que quita fences envolventes y colapsa saltos excesivos — patrón ya conocido en el proyecto (ver memoria: los fences de markdown ya causaron bugs con Gemini).

**Rationale**: "El chat está perfecto" → cero cambio de lógica conversacional; solo reubicación y garantía de presentación (FR-014/FR-015).

**Alternatives considered**: Modal fullscreen en vez de ruta — peor para compartir enlace y para el flujo de trabajo del analista.

## R7 — Explicaciones en catálogos

**Decision**: Mapa estático frontend `CATALOGO_DESCRIPCIONES` (código → {título, explicación en lenguaje simple, ejemplo}) para estados (Publicada, Cerrada, Adjudicada, Revocada, Suspendida…) y tipos (Licitación Pública L1/LE/LP/LQ/LR, Licitación Privada, Trato Directo…), contenido basado en las definiciones oficiales de ChileCompra. Al hacer click en una fila de `CatalogoPage`, se abre un `Drawer` de Ant Design con la explicación. Fallback para códigos sin descripción: "Sin descripción disponible".

**Rationale**: Los conceptos de Mercado Público son estables y públicos; un mapa estático evita migración, SP y endpoint para contenido que no cambia. Es lo único realista para mañana.

**Alternatives considered**: Columna `descripcion` en tablas de catálogo + SP — más "correcto" a largo plazo, pero requiere migración y seed para valor idéntico; puede migrarse después sin romper la UI.

## R8 — Sidebar: rol + avatar sin correo

**Decision**: En el footer del sidebar de `AppLayout.tsx`: avatar (foto si `user.fotoUrl` existe; si no, iniciales como hoy) + `user.nombre` + etiqueta de rol debajo (primer rol del usuario formateado, ej. "admin TIVIT"); se elimina la línea del email. El dropdown del header no cambia.

**Rationale**: El objeto `user` ya incluye roles en el JWT/`mpm_user`; no hay campo foto hoy, así que el avatar por iniciales sigue siendo el fallback (edge case del spec).

**Alternatives considered**: Hardcodear "admin TIVIT" — funciona para la demo pero rompe con otros roles; formatear el rol real cuesta lo mismo.

## R9 — Rediseño pantalla Licitaciones

**Decision**: Eliminar `LicitacionSearchBar.tsx` y el modo búsqueda inteligente (`useLicitacionBuscar` queda sin uso → se retira su consumo en la página; el endpoint backend no se toca); eliminar `SyncButton.tsx` del header. `LicitacionFilterBar` queda como única barra: input de búsqueda + Estado + Tipo + rango de fechas + botón **"Reiniciar filtros"** que resetea el estado de filtros al inicial. Espaciado: `Space size={20}` → layout compacto (`size={8-12}`), paddings de tarjetas reducidos, tabla con `size="small"`.

**Rationale**: Ataca exactamente los tres reclamos: duplicidad de buscadores ("¿cuál es la diferencia?"), funciones que confunden y espacio desperdiciado (FR-008..FR-011, SC-004).

**Alternatives considered**: Fusionar ambos buscadores en uno con toggle — mantiene la confusión que el cliente pidió eliminar.

## R10 — Investigación "por qué se ganan licitaciones" (US6, solo documento)

**Decision**: Entregable `docs/investigacion-victorias-licitaciones.md` que evalúe: (a) datos ya disponibles internamente (análisis históricos: `comparativa_puntajes`, factores de pérdida, adjudicatarios, montos); (b) API pública de Mercado Público (licitaciones adjudicadas por organismo/proveedor); (c) datos abiertos de ChileCompra (datasets de órdenes de compra y adjudicaciones); (d) señales extraíbles de actas (criterios de evaluación y ponderaciones). Conclusión requerida: viabilidad, limitaciones (sesgo de muestra, datos faltantes) y recomendación de siguiente paso. **Cero código.**

**Rationale**: El cliente marcó explícitamente "SOLO INVESTIGAR, NO APLICAR"; formalizarlo como entregable documental evita que se filtre implementación.

**Alternatives considered**: N/A — el alcance fue fijado por el cliente.
