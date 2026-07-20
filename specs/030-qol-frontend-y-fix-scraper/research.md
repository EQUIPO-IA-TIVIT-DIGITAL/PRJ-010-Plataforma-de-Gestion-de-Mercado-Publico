# Research: Ajustes QoL de Frontend + Fix Scraper "0 Resultados"

## 1. Causa raíz del scraper "0 licitaciones, código 0" (US3)

**Decision**: El bug no está en el wrapper .NET (`ScraperBackgroundService`, que ya detecta la anomalía vía `EsCicloExitoso`) sino en `tools/scraper-mp-v2/modulos/buscar.js`, en el bucle por estados de `buscarLicitaciones()`.

**Evidencia (código actual, líneas 41-97 de `buscar.js`)**:
- El bucle recorre 5 estados (`8,6,5,7,15`) con hasta 2 intentos cada uno. Si un estado falla ambos intentos (`errEstado` capturado), el `catch` interno solo hace `console.log('ADVERTENCIA: ...')` y continúa al siguiente estado — **nunca propaga el error hacia arriba**.
- Si los 5 estados fallan sus 2 intentos (p. ej. un problema transitorio de sesión, un cambio de estructura del sitio, o el postback colgado reapareciendo bajo una variante no cubierta por el retry existente), `licitacionesMap` queda vacío y la función retorna `[]` con éxito aparente.
- En `agente-mp.js` línea 101, `licitaciones.length === 0` se trata siempre como "no se encontraron licitaciones" (camino legítimo — ej. no hay licitaciones nuevas en el rango de fechas) y el ciclo cierra con `process.exit(0)` sin distinguir ese caso del caso "no se pudo leer ningún estado".
- Es la misma familia de bug que ya se corrigió una vez (postback colgado documentado en el comentario de líneas 42-47 y en la memoria de proyecto `project_scraper_postback_colgado`), pero el fix cubre el postback colgado *dentro* de un intento — no cubre "los 5 estados fallaron por cualquier motivo".

**Fix**: en `buscarLicitaciones()`, contar cuántos de los 5 estados terminaron con `exitoEstado === true`. Si **0 de 5** tuvieron éxito, lanzar un error (en vez de retornar `[]` silenciosamente) para que el `catch` de `executeCycle()` en `agente-mp.js` lo trate como fallo de ciclo (`process.exit(1)` + notificación de error), no como "0 resultados legítimos". Si al menos 1 de 5 tuvo éxito, el resultado combinado (aunque termine en 0 licitaciones únicas) sigue siendo confiable y puede tratarse como el camino normal.

**En el lado .NET**: `NotificarResultadoAsync` (ScraperBackgroundService.cs líneas 283-337) ya tiene la lógica de mensaje distinto para `EsCicloExitoso`; con el fix de arriba, un ciclo con 0/5 estados exitosos ya no reportará "código 0" (pasará a `exitCode != 0` y a la rama de notificación de error existente), cerrando la ambigüedad sin necesitar tocar el mensaje en sí.

**Alternatives considered**:
- *Reintentar el ciclo completo automáticamente ante 0 resultados*: descartado como fix principal — enmascararía el problema en vez de reportarlo, y el timer del `ScraperBackgroundService` ya reintenta en el próximo ciclo programado.
- *Comparar contra un mínimo histórico esperado de licitaciones*: más robusto a futuro pero requiere una baseline de datos que hoy no existe; se deja fuera de alcance de este spec (podría ser un hardening futuro tipo `021`/`028`).

## 2. Causa raíz de la fecha incorrecta en Notificaciones (US2)

**Decision**: La columna `notificaciones.created_at` es `TIMESTAMP` (sin zona horaria, `V064__Create_notificaciones.sql`), poblada con `CURRENT_TIMESTAMP` del servidor Postgres (contenedor Docker, típicamente en UTC). Npgsql mapea `timestamp without time zone` a `DateTime` con `Kind = Unspecified`, y `System.Text.Json` lo serializa sin sufijo `Z` (ej. `"2026-07-20T14:32:00"`). En el frontend, `NotificacionesPage.tsx` hace `new Date(fecha).toLocaleString('es-CL')` — `new Date("2026-07-20T14:32:00")` (sin `Z`) es interpretado por el navegador como **hora local del navegador**, no como UTC. Si el valor real es UTC y el navegador corre en horario de Chile (UTC-4 en invierno), el resultado mostrado queda desfasado ~4 horas respecto a la hora real del evento.

**Fix**: 
1. Backend: asegurar que el `DateTime` se serialice de forma inequívoca como UTC (marcar `DateTimeKind.Utc` al leer de Postgres, o exponer el campo con sufijo `Z`/formato ISO-8601 con offset).
2. Frontend: convertir explícitamente a la zona horaria de Chile (`America/Santiago`) al formatear, en vez de depender de la zona horaria implícita del navegador — usando el plugin de timezone de `dayjs` (ya es dependencia del proyecto).

**Alternatives considered**:
- *Migrar la columna a `TIMESTAMPTZ`*: es la corrección más robusta a nivel de esquema, pero requiere una migración `VXXX` de tipo de columna sobre una tabla en producción; se evalúa en el plan técnico si el ajuste de serialización (opción elegida) es suficiente sin tocar el esquema, dado que es de menor riesgo para una entrega de QoL.

## 3. Orden y filtro por fecha en /analisis (US4)

**Decision**: `usp_AnalisisWorkspaces_Listar` (`V052__Analisis_Create_usp_Workspaces.sql`) ya ordena `ORDER BY f.created_at DESC` — el requisito de "más reciente primero" (FR-008) ya está resuelto a nivel de datos; solo falta confirmar que el frontend no reordena la respuesta y que el filtro de fecha (FR-010, nuevo) se agregue como parámetros opcionales `p_fecha_desde` / `p_fecha_hasta` sobre `aw.created_at`, siguiendo el mismo patrón que `p_search` / `p_estado` ya existentes en el mismo SP.

**Decision**: FR-009 (fecha visible en cada fila) se resuelve en el frontend — `WorkspaceItemDto` ya expone `created_at`, solo falta agregarlo como columna visible en `AnalisisListPage.tsx`.

## 4. Patrones visuales para los rediseños (US1, US5, US6, US7)

**Decision**: Reutilizar los componentes y patrones ya establecidos por los rediseños previos (`019-rediseno-frontend`, `025-rediseno-chat-analisis-antd-x`) — Ant Design 5 + `@ant-design/icons`, sin introducir una librería de componentes nueva. Los rediseños de `/analisis/:id`, `/analisis/:id/dashboard` y `/alertas` deben mantenerse dentro de ese mismo sistema visual para no crear una tercera "capa" de estilo en el frontend.

**Alternatives considered**: adoptar un nuevo design system o librería de componentes — descartado, fuera de alcance para un spec de QoL y contradice la Assumption ya registrada en `spec.md`.
