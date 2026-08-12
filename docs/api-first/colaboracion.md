# API Specification: Colaboración (Licitaciones de Interés)

> Módulo `MPM.Modules.Colaboracion` — flujo colaborativo go/no-go (spec 031):
> marcar una licitación como "de interés", vincularla al workspace de análisis
> y a una conversación de equipo. Tabla base: V122.

## 1. Scope

### Included
- Marcar una licitación como de interés (idempotente)
- Consultar el estado de interés de una licitación
- Listar todas las licitaciones marcadas con su estado actual
- Vinculación automática con workspace de análisis y conversación de equipo

### Excluded
- Comentarios o asignación propios (se reutilizan conversaciones/mensajes de Mensajería)
- Workflow de aprobación go/no-go (el flag es el estado de la licitación)

## 2. Data Model

```mermaid
erDiagram
    licitaciones_interes {
        bigint id PK
        bigint licitacion_id FK UK
        bigint workspace_id FK
        bigint conversacion_id FK
        varchar marcado_por
        smallint estado_licitacion_al_marcar
        timestamp created_at
        timestamp updated_at
    }

    licitaciones_interes }o--|| licitaciones : "licitacion_id"
    licitaciones_interes }o--o| analisis_workspaces : "workspace_id"
    licitaciones_interes }o--o| conversaciones : "conversacion_id"
```

## 3. Endpoints — `[Authorize]`

| Método | Ruta | Descripción |
|--------|------|-------------|
| POST | `/api/v1/licitaciones/{licitacionId}/interes` | Marca como de interés (idempotente) |
| GET | `/api/v1/licitaciones/{licitacionId}/interes` | Estado de interés de la licitación |
| GET | `/api/v1/licitaciones/interes` | Listado de licitaciones marcadas (con estado actual) |

## 4. Stored procedures (V122)

| SP | Descripción |
|----|-------------|
| `usp_LicitacionesInteres_Marcar` | UPSERT idempotente + captura estado al marcar |
| `usp_LicitacionesInteres_ObtenerPorLicitacion` | Detalle por licitación |
| `usp_LicitacionesInteres_VincularWorkspace` | Link al workspace de análisis |
| `usp_LicitacionesInteres_VincularConversacion` | Link a la conversación de equipo |
| `usp_LicitacionesInteres_Listar` | Listado con nombre y estado actual |

## 5. Reglas de negocio

- `UNIQUE (licitacion_id)` garantiza una sola fila por licitación (FR-013).
- Al marcar se captura `estado_licitacion_al_marcar`; la UI compara con el
  estado actual para señalar cambios desde el marcado.
- El módulo no crea conversaciones: la UI las crea vía Mensajería y luego
  vincula con `VincularConversacion`.
