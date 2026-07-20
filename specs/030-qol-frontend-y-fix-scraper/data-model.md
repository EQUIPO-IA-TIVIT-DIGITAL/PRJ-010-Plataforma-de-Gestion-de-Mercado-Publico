# Data Model: Ajustes QoL de Frontend + Fix Scraper "0 Resultados"

No se introducen entidades nuevas. Este spec ajusta el comportamiento y la representación de entidades existentes.

## Ranking de competidor (`DashboardEjecutivoDto` / `RankingCompetidorDto`)

- **Campos existentes sin cambio de forma**: `nombreCompetidor`, `vecesCompetidor`, `vecesGanador`, `montoTotalGanado`, tabla de detalle por licitación (`licitacion`, `resultadoTivit`, `montoAdjudicado`).
- **Cambio de comportamiento (US1)**: la etiqueta que acompaña `vecesGanador` en el frontend debe dejar explícito que se trata de victorias **del competidor**, no de TIVIT (FR-001). Si `vecesGanador == 0`, el frontend no renderiza esa etiqueta (FR-003). No requiere cambios en el DTO ni en el SP — es un ajuste de presentación.

## Notificación (`NotificacionDto`)

- **Campo afectado**: `CreatedAt` (`DateTime`, mapeado desde `notificaciones.created_at TIMESTAMP` sin zona horaria).
- **Cambio de comportamiento (US2)**: el valor debe quedar marcado inequívocamente como UTC al salir de la API (`DateTimeKind.Utc` + serialización con offset), y el frontend debe convertirlo explícitamente a `America/Santiago` en vez de asumir la zona horaria del navegador. No cambia el esquema de la tabla en este spec (ver research.md §2, alternativa descartada).
- **Nuevo comportamiento de negocio (US3, vía FR-007)**: la notificación de resultado del ciclo del scraper debe distinguir "0 licitaciones nuevas, ciclo saludable" (mensaje/severidad normal) de "no se pudo leer ningún estado de búsqueda" (mensaje/severidad de error) — hoy ambos casos comparten el mismo texto ambiguo. El campo `Tipo`/severidad existente en `NotificacionDto` se reutiliza para esta distinción; no requiere un campo nuevo.

## Workspace de análisis (`WorkspaceItemDto`)

- **Campos existentes reutilizados**: `id`, `nombre`, `licitacionNombre`, `estado`, `documentosCount`, `ultimoAnalisisFecha`, `createdAt`, `totalCount` (paginación).
- **Cambio de comportamiento (US4)**:
  - `createdAt` pasa a mostrarse como columna visible en la lista (FR-009) — ya viene en el DTO, es un cambio de presentación.
  - El endpoint de listado (`GET /api/v1/analisis/workspaces`) gana dos parámetros de consulta opcionales, `fechaDesde` y `fechaHasta` (FR-010), que se traducen a `p_fecha_desde` / `p_fecha_hasta` en `usp_AnalisisWorkspaces_Listar`, filtrando por `aw.created_at`. El orden por defecto (`created_at DESC`, FR-008) ya existe en el SP y no cambia.

## Ciclo de scraper (interno, sin DTO expuesto)

- **Cambio de comportamiento (US3, FR-006)**: `buscarLicitaciones()` en `tools/scraper-mp-v2/modulos/buscar.js` pasa a rastrear cuántos de los 5 estados de búsqueda tuvieron éxito. Si 0 de 5 tuvieron éxito, la función lanza un error en lugar de retornar un arreglo vacío — esto hace que `executeCycle()` en `agente-mp.js` trate el ciclo como fallo (`process.exit(1)`) en vez de éxito con 0 resultados. No es una entidad de datos, pero es el cambio de comportamiento central de US3 y determina qué recibe `NotificarResultadoAsync` en el lado .NET.
