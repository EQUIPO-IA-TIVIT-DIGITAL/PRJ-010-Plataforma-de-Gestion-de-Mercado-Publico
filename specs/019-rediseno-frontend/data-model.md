# Data Model: Rediseño Frontend de MPM — Alcance por pantalla

## Componentes de UI compartidos

### `StatusBadge`

| Prop | Tipo | Requerido | Descripción |
|---|---|---|---|
| `variant` | `'neutral' \| 'info' \| 'warning' \| 'success' \| 'error' \| 'tertiary'` | Sí | Determina el color, tomado del theme de `main.tsx` — nunca un color libre. |
| `label` | `string` | Sí | Texto visible del badge (ej. "Adjudicada", "Pendiente"). |
| `icon` | `React.ReactNode` | No | Ícono opcional de `@ant-design/icons`, nunca un emoji (FR-007). |

**Reemplaza**: `STATUS_CONFIG` de `AnalisisListPage.tsx` y `CatalogoPage.tsx`, los ternarios inline de `NotificacionesPage.tsx`, el uso de `<Tag color="success"/"error">` de `EjecutivoDashboardPage.tsx`, y cualquier badge de estado nuevo en Alertas/Competidores.

### `PageHeader`

| Prop | Tipo | Requerido | Descripción |
|---|---|---|---|
| `icon` | `React.ReactNode` | Sí | Ícono de `@ant-design/icons`, se renderiza en un chip con `colorPrimary` del theme. |
| `title` | `string` | Sí | Título de la pantalla. |
| `subtitle` | `string` | No | Texto secundario (ej. conteo de resultados). |
| `actions` | `React.ReactNode` | No | Slot para botones de acción a la derecha (ej. "Sincronizar"). |

**Reemplaza**: el patrón de ícono-con-gradiente de `LicitacionesPage.tsx`/`AnalisisListPage.tsx` y el `Typography.Title` suelto de `NotificacionesPage.tsx`.

## Entidad de datos nueva (solo si Ejecutivo FR-008 se implementa según research.md §3)

### `CoberturaMercadoEjecutivo` (respuesta de `GET /api/v1/analisis/ejecutivo/cobertura-mercado`)

| Campo | Tipo | Descripción |
|---|---|---|
| `areaCodigo` | `number \| null` | Área de negocio filtrada, o `null` para todas. |
| `fechaDesde` / `fechaHasta` | `string (ISO date)` | Rango del período evaluado. |
| `totalLicitacionesMercado` | `number` | Universo total de licitaciones detectadas en el área/período (vía búsqueda pública, mismo mecanismo que `competidor-mercado.js`). |
| `totalLicitacionesTivit` | `number` | Cuántas de esas licitaciones TIVIT efectivamente analizó u ofertó. |
| `porcentajeCobertura` | `number` | `totalLicitacionesTivit / totalLicitacionesMercado * 100`, redondeado. |
| `licitacionesSinParticipacion` | `{ codigo: string, nombre: string, organismo: string, fechaCierre: string }[]` | Listado de licitaciones del área donde TIVIT no participó — para acción directa (ver detalle, marcar de interés). |

**Origen de los datos**: reutiliza la infraestructura de spec 024 (`buscarPublico.js`, tabla `competidores_actividad_mercado` como referencia de patrón, no como fuente directa — esta consulta es sobre TIVIT mismo, no sobre un competidor). Sin tabla nueva; el stored procedure agrega sobre `licitaciones` (universo) y `licitaciones_ofertas`/`analisis_workspaces` (participación de TIVIT).

**Validación**: `fechaHasta` no puede ser anterior a `fechaDesde` (mismo patrón de validación que `ActividadMercadoRequest` de `CompetidoresController.cs`).
