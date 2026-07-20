# Contratos afectados

Solo se documentan los contratos que cambian. El resto de los endpoints usados por este spec (dashboard ejecutivo, notificaciones, alertas, workspace/dashboard de análisis) se consumen sin cambio de forma — el trabajo ahí es de presentación en el frontend.

## `GET /api/v1/analisis/workspaces` — nuevos parámetros de filtro por fecha (US4 / FR-010)

**Antes**:

```
GET /api/v1/analisis/workspaces?page=1&pageSize=20&search=&estado=
```

**Después** (agrega dos parámetros opcionales, resto sin cambio):

```
GET /api/v1/analisis/workspaces?page=1&pageSize=20&search=&estado=&fechaDesde=2026-07-01&fechaHasta=2026-07-20
```

- `fechaDesde` (opcional, `date` en formato `YYYY-MM-DD`): filtra workspaces con `created_at >= fechaDesde` (inicio del día).
- `fechaHasta` (opcional, `date` en formato `YYYY-MM-DD`): filtra workspaces con `created_at <= fechaHasta` (fin del día).
- Si se omiten ambos, el comportamiento es idéntico al actual (sin filtro de fecha).
- El orden de la respuesta (`created_at DESC`) no cambia.
- Response shape (`ApiResponse<PaginatedResult<WorkspaceItemDto>>`) no cambia.

## Notificación de resultado del scraper — severidad distinta para "0 licitaciones sin lectura válida" (US3 / FR-007)

No cambia la forma del `NotificacionDto` (`tipo`, `titulo`, `mensaje`, `metadata`, `createdAt`). Cambia el **contenido** que emite `ScraperBackgroundService.NotificarResultadoAsync` según el nuevo comportamiento de `agente-mp.js`:

| Caso | Antes | Después |
|---|---|---|
| Ciclo normal, licitaciones nuevas procesadas | Notificación de éxito, detalle de conteos | Sin cambio |
| Ciclo normal, 0 licitaciones nuevas pero al menos 1 de 5 estados leyó bien el sitio | Notificación ambigua ("El scraper terminó con código 0...") | Notificación de éxito neutro ("Sin licitaciones nuevas en este ciclo") |
| 0 de 5 estados pudieron leer el sitio (falla real) | Mismo mensaje ambiguo que el caso anterior — indistinguible | `exitCode != 0`, entra a la rama de notificación de error ya existente en `NotificarResultadoAsync` (falla de ciclo) |

## Notificaciones — timestamp serializado como UTC explícito (US2 / FR-004, FR-005)

`GET /api/v1/notificaciones` — el campo `createdAt` de cada item pasa de un `DateTime` ambiguo (sin offset) a un valor serializado como UTC explícito (ISO-8601 con `Z` u offset). El frontend debe convertirlo a `America/Santiago` al mostrarlo, en vez de dejar que el navegador lo interprete como hora local. No cambia ningún otro campo del contrato.
