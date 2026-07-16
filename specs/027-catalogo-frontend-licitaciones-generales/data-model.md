# Data Model: Frontend de Licitaciones Alineado al Catálogo Real

**Feature**: `027-catalogo-frontend-licitaciones-generales`

## Cambios de esquema

### `tipos_licitacion` (cambia el tipo de la clave)

| Campo | Antes | Después |
|---|---|---|
| `codigo` | `SMALLINT PRIMARY KEY` (1-4) | `VARCHAR(10) PRIMARY KEY` (LE, LP, LQ, LR, CO, CA, TD, LS, L, B, R, E, I, O, H, CI, DC) |
| `nombre` | sin cambio | sin cambio |
| `slug` | sin cambio (se conserva para compatibilidad de UI existente, ya no es la clave de filtrado) | sin cambio |
| `descripcion` | sin cambio | `'Pendiente de documentar'` para los 4 códigos nuevos (O, H, CI, DC) sin definición oficial confirmada todavía |

Contenido según el glosario de `026-robustez-sincronizacion-tipos-reales` (spec.md, sección "Glosario de Tipos de Licitación").

### `estados_licitacion` (sin cambio de esquema ni de datos)

No se modifica la tabla. Se ajusta únicamente `usp_Catalogos_EstadosLicitacion()` para filtrar y devolver solo los 5 códigos reales vigentes (5, 6, 7, 8, 15) — los códigos heredados (1-4) permanecen en la tabla intactos, dado que el código 1 está en uso activo por 144 licitaciones como fallback intencional (spec 026, FR-007).

## DTOs afectados

### Backend (`MPM.Modules.Catalogo`)

- `TipoLicitacionItemDto.Codigo`: `int` → `string`.

### Frontend (`src/mpm-web/src/types/catalogo.ts`)

- `TipoLicitacionItem.codigo`: `number` → `string`.

### Frontend (`src/mpm-web/src/types/licitacion.ts`)

- `LicitacionFilter.tipo`: hoy tipado como `TipoLicitacion | null` (unión fija de 4 valores genéricos: `LICITACION | TRATO_DIRECTO | CONVENIO_MARCO | COMPRA_AGIL`). Pasa a `string | null` para aceptar cualquier código real del catálogo, sin una unión cerrada que quedaría desactualizada cada vez que aparezca un código nuevo.

## Sin cambios

- `licitaciones.tipo` (columna VARCHAR(30) libre, sin FK) — no requiere migración de datos existentes.
- `licitaciones.codigo_estado` y su FK hacia `estados_licitacion` — no requiere migración de datos existentes.
- `LicitacionResumenDto` / `LicitacionDetalleDto` (backend) — no cambian de forma; el cambio es de qué columnas renderiza la tabla del frontend, no de qué trae la API.
- `LicitacionNaturalSearchResult` — no afectado por esta spec.
