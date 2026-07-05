# Research: Fase 4 — Notificaciones y Seguimiento Activo

**Feature**: MPM CU010 — Fase 4: Seguimiento de Licitaciones Activas con Alertas (US3)
**Date**: 2026-06-24
**Spec**: [spec.md](./spec.md)

---

## 1. Estado actual del módulo de Notificaciones

### ✅ Ya implementado (no requiere cambios)

| Componente | Archivo | Estado |
|---|---|---|
| Tabla `notificaciones` | V064 | Completa — `tipo`, `titulo`, `mensaje`, `metadata` JSONB, `leido`, `usuario_id` |
| SPs de notificaciones | V065 + V068 | `usp_Notificaciones_Crear`, `_Listar`, `_ContarNoLeidas`, `_MarcarLeida`, `_MarcarTodasLeidas` |
| `NotificacionesService.CrearAsync()` | `Services/NotificacionesService.cs` | Funcional — ya usado por `ScraperBackgroundService` |
| `NotificacionesController` | `Controllers/NotificacionesController.cs` | 4 endpoints: listar, contar, marcar leída, marcar todas leídas |
| `NotificacionBell` (frontend) | `components/NotificationBell.tsx` | Polling del count cada 30s — funcional |
| `NotificacionesPage` | `pages/NotificacionesPage.tsx` | Lista paginada con `soloNoLeidas` — funcional |

### ❌ Falta para US3

| Componente | Estado |
|---|---|
| Tabla `licitaciones_seguidas` | Pendiente — V072 |
| Tabla `licitaciones_aclaraciones` | Pendiente — V072 |
| SPs de seguimiento | Pendiente — V073 |
| `ApiMpLicitacion.Preguntas` field | Pendiente — ampliar model existente |
| `AclaracionMonitorService` (background) | Pendiente |
| Endpoints seguir/dejar licitación | Pendiente |
| Frontend: botón "Seguir" en LicitacionesPage | Pendiente |

---

## 2. Decisiones de diseño

### Decisión 1: ¿El MP API devuelve aclaraciones?

**Verificado**: El endpoint de detalle de la API pública de Mercado Público (`?ticket=...&codigo=...`) devuelve un campo `Preguntas.Listado[]` con las preguntas/aclaraciones:

```json
{
  "Listado": [{
    "CodigoExterno": "1234-56-LP24",
    "Preguntas": {
      "Listado": [{
        "CodigoAclaracion": 1,
        "Pregunta": "¿Cuál es el plazo de entrega requerido?",
        "Respuesta": "El plazo es de 30 días hábiles.",
        "FechaPublicacion": "2025-06-15T10:00:00",
        "FechaRespuesta": "2025-06-16T09:00:00"
      }]
    }
  }]
}
```

`CodigoAclaracion` es secuencial por licitación. Sirve como clave de idempotencia para evitar notificaciones duplicadas.

**`ApiMpLicitacion` actual**: No tiene campo `Preguntas`. Se agrega campo `Preguntas` al modelo existente.

---

### Decisión 2: ¿Seguimiento automático vs. manual?

**Alternativas evaluadas:**

| Opción | Ventaja | Desventaja |
|---|---|---|
| **A: Usuario marca manualmente** | Simple, user in control, no lógica de inferencia | Requiere acción del usuario; puede olvidarse |
| **B: Auto-detectar de `licitaciones_adjuntos`** | Cero fricción para el usuario | Compleja: solo detecta licitaciones *ya scrapeadas*, no las activas sin acta |
| **C: Hybrid: auto para las del scraper + manual para el resto** | Completo | Más complejo, Fase 4 sería más larga |

**Decisión: Opción A para Fase 4**. El usuario marca manualmente desde la lista de licitaciones. La Opción B o C puede implementarse en Fase 5 como mejora.

**Rationale**: La lista `/licitaciones` ya es visible y usada. Agregar un botón "Seguir" (icono estrella) es UX natural y conocida.

---

### Decisión 3: ¿Frecuencia de polling?

**Restricción**: SC-005 exige notificación dentro de 60 min de que aparece en MP.

**Análisis**: 
- Una aclaración tarda minutos en publicarse después de que el organismo la envía.
- El endpoint de detalle MP no tiene webhooks → polling es la única opción.
- Rate limit estimado: sin documentación oficial, 1 req/seg es conservador y suficiente.
- Con 50 licitaciones seguidas: 50 requests → 50 seg por ciclo.

**Decisión**: Ciclo cada 30 minutos (configurable vía `MONITOR_INTERVAL_MINUTES`). Cumple SC-005 con margen.

---

### Decisión 4: ¿Dónde vive `AclaracionMonitorService`?

El servicio necesita:
- Acceder a `licitaciones_seguidas` (nuevo)
- Llamar a `ApiMpService.GetDetalleAsync()` (ya en Licitaciones)
- Llamar a `NotificacionesService.CrearAsync()` (cross-module)

**Decisión**: Vive en `MPM.Modules.Licitaciones/Services/AclaracionMonitorService.cs`. Inyecta `NotificacionesService` vía DI (ya está registrado globalmente). Patrón idéntico a `SyncEngineService`.

---

### Decisión 5: Estados activos a monitorear

| Código Estado | Descripción MP | ¿Monitorear? |
|---|---|---|
| 1 | Publicada | ✅ |
| 2 | Cerrada | ✅ (aclaraciones pueden seguir llegando) |
| 3 | Desierta | ❌ |
| 4 | Publicada con Preguntas | ✅ |
| 5 | Adjudicada | ❌ |
| 6 | Revocada | ❌ |
| 7 | Suspendida | ❌ |
| 8 | Adjudicada (variante) | ❌ |

Monitor filtra: `estado IN (1, 2, 4)`.

---

### Decisión 6: ¿Notificar a todos los seguidores o solo al que la marcó?

**Decisión**: Notificar a **todos** los `usuario_id` que siguen una licitación cuando aparece aclaración nueva.

**Rationale**: Una aclaración afecta a todo el equipo TIVIT que trabaja esa licitación. Cada usuario puede dejar de seguirla si no quiere más alertas.

---

## 3. SPs necesarios

### V072 — Tablas

```sql
-- licitaciones_seguidas
CREATE TABLE IF NOT EXISTS licitaciones_seguidas (
    id            BIGSERIAL PRIMARY KEY,
    usuario_id    TEXT NOT NULL,
    licitacion_id BIGINT NOT NULL REFERENCES licitaciones(id),
    created_at    TIMESTAMP NOT NULL DEFAULT NOW(),
    UNIQUE(usuario_id, licitacion_id)
);

-- licitaciones_aclaraciones
CREATE TABLE IF NOT EXISTS licitaciones_aclaraciones (
    id                BIGSERIAL PRIMARY KEY,
    licitacion_id     BIGINT NOT NULL REFERENCES licitaciones(id),
    codigo_aclaracion INT NOT NULL,
    pregunta          TEXT,
    respuesta         TEXT,
    fecha_publicacion TIMESTAMP,
    fecha_respuesta   TIMESTAMP,
    notificado        BOOLEAN NOT NULL DEFAULT FALSE,
    created_at        TIMESTAMP NOT NULL DEFAULT NOW(),
    UNIQUE(licitacion_id, codigo_aclaracion)
);
```

### V073 — Stored Procedures

- `usp_Licitaciones_SeguirToggle(p_usuario_id, p_licitacion_id)` → `p_accion TEXT ('seguida'|'no_seguida')`, `p_error_msg`
- `usp_Licitaciones_EsSeguida(p_usuario_id, p_licitacion_id)` → `p_es_seguida BOOL`
- `usp_Licitaciones_ObtenerParaMonitor(p_estados INT[])` → tabla con `(licitacion_id, codigo_externo, nombre, usuario_ids TEXT[])`
- `usp_Licitaciones_Aclaracion_Upsert(p_licitacion_id, p_codigo, p_pregunta, p_respuesta, p_fecha_pub, p_fecha_resp)` → `p_es_nueva BOOL`, `p_id BIGINT`
- `usp_Licitaciones_ObtenerSeguidas(p_usuario_id)` → listado de licitaciones que sigue el usuario

---

## 4. Extensión del modelo `ApiMpLicitacion`

Agregar a `ApiMpService.cs`:

```csharp
[JsonPropertyName("Preguntas")]
public ApiMpPreguntas? Preguntas { get; set; }

public class ApiMpPreguntas
{
    [JsonPropertyName("Listado")]
    public List<ApiMpAclaracion>? Listado { get; set; }
}

public class ApiMpAclaracion
{
    [JsonPropertyName("CodigoAclaracion")]
    public int CodigoAclaracion { get; set; }

    [JsonPropertyName("Pregunta")]
    public string? Pregunta { get; set; }

    [JsonPropertyName("Respuesta")]
    public string? Respuesta { get; set; }

    [JsonPropertyName("FechaPublicacion")]
    public string? FechaPublicacion { get; set; }

    [JsonPropertyName("FechaRespuesta")]
    public string? FechaRespuesta { get; set; }
}
```

---

## 5. Tipo de notificación nuevo

Tipo: `aclaracion_detectada`

```json
{
  "tipo": "aclaracion_detectada",
  "titulo": "Nueva aclaración en licitación 1234-56-LP24",
  "mensaje": "¿Cuál es el plazo de entrega requerido? (respuesta disponible)",
  "metadata": {
    "licitacion_id": 42,
    "codigo_externo": "1234-56-LP24",
    "codigo_aclaracion": 3,
    "tiene_respuesta": true
  }
}
```

La `metadata` permite que el frontend renderice un enlace directo a la licitación.

---

## 6. Resumen pendiente — visión general del proyecto

| Fase | Estado |
|---|---|
| Fase 0 — Auth, Catálogo, Mensajería | ✅ Completa |
| Fase 1 — Pipeline Análisis Manual | ✅ Completa |
| Fase 2 — Automatización Scraping | ✅ Completa |
| Fase 3 — Dashboard Ejecutivo | ✅ Completa |
| **Fase 4 — Notificaciones Seguimiento** | 🔴 Pendiente |
| Fase 5 — Despliegue GCP | 🔴 Pendiente |
