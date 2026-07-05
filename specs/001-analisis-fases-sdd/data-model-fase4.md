# Data Model: Fase 4 — Seguimiento y Alertas de Aclaraciones

**Feature**: MPM CU010 — Fase 4
**Date**: 2026-06-24

---

## Entidades existentes (sin cambios)

### `licitaciones` (V002)
- Usada para filtrar por `codigo_estado IN (1, 2, 4)` — estados activos
- `codigo_externo` se usa para llamar al endpoint de detalle de MP API

### `notificaciones` (V064)
- Recibe notificaciones tipo `aclaracion_detectada` creadas por `AclaracionMonitorService`
- Ya tiene `metadata JSONB` para almacenar el link a la licitación

---

## Nuevas entidades — V072

### `licitaciones_seguidas`

```sql
id            BIGSERIAL PRIMARY KEY
usuario_id    TEXT NOT NULL              -- JWT user_id
licitacion_id BIGINT NOT NULL            -- FK → licitaciones(id)
created_at    TIMESTAMP DEFAULT NOW()
UNIQUE(usuario_id, licitacion_id)
```

**Índices**: `(usuario_id)`, `(licitacion_id)`

**Invariantes**:
- Un usuario puede seguir la misma licitación solo una vez (UNIQUE).
- El toggle (seguir/dejar de seguir) hace INSERT o DELETE según el estado actual.
- No tiene `record_status` — la ausencia de fila = no seguida. DELETE limpio.

---

### `licitaciones_aclaraciones`

```sql
id                BIGSERIAL PRIMARY KEY
licitacion_id     BIGINT NOT NULL        -- FK → licitaciones(id)
codigo_aclaracion INT NOT NULL           -- CodigoAclaracion de la API MP (secuencial)
pregunta          TEXT                   -- Texto de la pregunta
respuesta         TEXT                   -- Texto de la respuesta (NULL si pendiente)
fecha_publicacion TIMESTAMP             -- FechaPublicacion en MP
fecha_respuesta   TIMESTAMP             -- FechaRespuesta en MP (NULL si no respondida)
notificado        BOOLEAN DEFAULT FALSE  -- TRUE después de enviar la notificación
created_at        TIMESTAMP DEFAULT NOW()
UNIQUE(licitacion_id, codigo_aclaracion)
```

**Índices**: `(licitacion_id)`, `(notificado)` donde `notificado = FALSE`

**Invariantes**:
- `UNIQUE(licitacion_id, codigo_aclaracion)`: garantiza idempotencia — el monitor puede reejecutar sin duplicar notificaciones.
- `notificado` se pone en `TRUE` después de crear la notificación — así un ciclo fallido puede recuperarse sin renotificar.

---

## Flujo de datos (Fase 4 end-to-end)

```
Usuario en /licitaciones
    ↓ clic "Seguir" (estrella icon)
POST /api/v1/licitaciones/{id}/seguir
    ↓ usp_Licitaciones_SeguirToggle()
licitaciones_seguidas (DB)

── cada 30 min ──────────────────────────────────────────

AclaracionMonitorService (BackgroundService)
    ↓ usp_Licitaciones_ObtenerParaMonitor(estados=[1,2,4])
[ {licitacion_id, codigo_externo, usuario_ids[]}, ... ]
    ↓ ApiMpService.GetDetalleAsync(codigo_externo, ticket)
ApiMpLicitacion.Preguntas.Listado[]
    ↓ para cada CodigoAclaracion
    ↓ usp_Licitaciones_Aclaracion_Upsert()
    ↓ si p_es_nueva = TRUE:
    ↓ para cada usuario_id en usuario_ids[]:
NotificacionesService.CrearAsync(
    usuarioId, "aclaracion_detectada",
    "Nueva aclaración en ...",
    "Pregunta: ...",
    metadata: { licitacion_id, codigo_externo, codigo_aclaracion, tiene_respuesta }
)
notificaciones (DB)
    ↓ NotificationBell (polling 30s)
Frontend — badge de campana se incrementa
    ↓ usuario abre /notificaciones
NotificacionesPage — muestra notificación con link a /licitaciones?codigo=...
```

---

## SPs nuevos — V073

| Stored Procedure | Input | Output | Uso |
|---|---|---|---|
| `usp_Licitaciones_SeguirToggle` | `p_usuario_id, p_licitacion_id` | `p_accion TEXT, p_error_msg` | Toggle seguir/dejar |
| `usp_Licitaciones_EsSeguida` | `p_usuario_id, p_licitacion_id` | `p_es_seguida BOOL` | Check estado desde endpoint GET |
| `usp_Licitaciones_ObtenerParaMonitor` | `p_estados INT[]` | tabla `(licitacion_id, codigo_externo, nombre, usuario_ids TEXT[])` | Monitor loop |
| `usp_Licitaciones_Aclaracion_Upsert` | `p_licitacion_id, p_codigo, p_pregunta, p_respuesta, p_fecha_pub, p_fecha_resp` | `p_es_nueva BOOL, p_id BIGINT` | Idempotent insert de aclaración |
| `usp_Licitaciones_ObtenerSeguidas` | `p_usuario_id` | tabla con licitaciones seguidas (join con `licitaciones`) | Endpoint GET /seguidas |

---

## Sin cambios en migraciones existentes

No se modifican V001–V071. Solo se agregan V072 (tablas) y V073 (SPs).
