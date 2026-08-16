# API Spec — Decisiones GO/NO GO (DEC)

**Versión**: 1.0
**Módulo**: Decisiones (DEC) — `MPM.Modules.Colaboracion` (evolución spec 031 / V122)
**Generado por**: api-first-spec
**Fecha**: 2026-08-16
**Rama**: `036-flujo-comercial-ofertas`
**Diseño origen**: [docs/design/flujo-ofertas.md](../design/flujo-ofertas.md) (§11, D6)
**HUs de origen**: pendientes

---

## 1. Scope

### Included
- Decisión formal GO/NO GO de una licitación por el gerente (siempre humana), con motivo.
- Snapshot de la recomendación IA (`recomendacion_ia` + `score_confianza`) copiado desde el
  último análisis comercial completado (`analisis_licitacion_comercial`, V142) **al momento
  de decidir** — la decisión queda inmutable frente a re-análisis posteriores.
- Consulta del estado de la decisión para la ficha de la licitación.
- Evolución de `licitaciones_interes` (V122): la fila de interés pasa a contener la decisión
  (1 fila por licitación; re-decidir reemplaza).

### Excluded
- Notificación a personas marcadas (`notificados`): se completa en **Fase 3** (generador de
  propuesta + avisos).
- Historial de decisiones (auditoría de cambios de decisión): sin historial en esta fase
  (la tabla es 1:1 por licitación; re-decidir reemplaza).
- Recomendación IA en sí: vive en el análisis comercial (spec analisis-comercial.md, V142).
- Match de capacidades: spec [censo.md](censo.md).

---

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
        varchar decision "go|no_go (decisión humana)"
        text motivo "obligatorio en NO GO"
        varchar recomendacion_ia "snapshot: strong_go|go|no_go|strong_no_go"
        numeric score_confianza "snapshot 0-1"
        varchar decidido_por "email del gerente (JWT)"
        timestamp decidido_at
        jsonb notificados "emails/ids elegidos a mano — Fase 3"
        timestamp created_at
        timestamp updated_at
    }
    licitaciones_interes }o--|| licitaciones : "licitacion_id"
    analisis_licitacion_comercial ||--o{ licitaciones_interes : "snapshot al decidir"
```

### Table: licitaciones_interes — evolución V122 + V144

| Column | Type | Nullable | Default | Description |
|--------|------|----------|---------|-------------|
| id | BIGSERIAL | NO | — | PK |
| licitacion_id | BIGINT | NO | — | FK → licitaciones(id), **UK** (1 fila por licitación) |
| workspace_id | BIGINT | YES | NULL | FK → analisis_workspaces (spec 031) |
| conversacion_id | BIGINT | YES | NULL | FK → conversaciones (spec 031) |
| marcado_por | VARCHAR(200) | YES | NULL | Email de quien marcó interés |
| estado_licitacion_al_marcar | SMALLINT | NO | — | Estado de la licitación al marcar (catálogo estados_licitacion) |
| decision | VARCHAR(20) | YES | NULL | **Nuevo (V144)**: `go` \| `no_go` (decisión humana) |
| motivo | TEXT | YES | NULL | **Nuevo (V144)**: justificación; obligatorio si `no_go` |
| recomendacion_ia | VARCHAR(20) | YES | NULL | **Nuevo (V144)**: snapshot `strong_go\|go\|no_go\|strong_no_go` |
| score_confianza | NUMERIC(4,3) | YES | NULL | **Nuevo (V144)**: snapshot 0-1 |
| decidido_por | VARCHAR(200) | YES | NULL | **Nuevo (V144)**: email del gerente (JWT) |
| decidido_at | TIMESTAMP | YES | NULL | **Nuevo (V144)**: momento de la decisión |
| notificados | JSONB | YES | NULL | **Nuevo (V144)**: personas elegidas a mano — se completa en Fase 3 |
| created_at / updated_at | TIMESTAMP | NO | CURRENT_TIMESTAMP | Auditoría |

Indexes: PK `id` · UK `licitacion_id`.

---

## 3. Required Catalogs

### Enum: Decision (application-level, no se almacena como catálogo)

| Value | Description |
|-------|-------------|
| `go` | TIVIT oferta: habilita la generación de propuesta (Fase 3) |
| `no_go` | TIVIT no oferta: motivo obligatorio, cierra el expediente |

### Enum: RecomendacionIA (application-level — proviene de V142, se copia como snapshot)

| Value | Description |
|-------|-------------|
| `strong_go` / `go` | Recomendación IA favorable |
| `no_go` / `strong_no_go` | Recomendación IA desfavorable |

> La IA **recomienda**, nunca decide (regla transversal del flujo).

---

## 4. State Flow

| Estado | Acción | Siguiente | Condiciones |
|--------|--------|-----------|-------------|
| (sin fila) | `POST /decision` | go | Decisión humana, body `{decision:"go"}` |
| (sin fila) | `POST /decision` | no_go | Decisión humana, `{decision:"no_go", motivo:"..."}` — motivo obligatorio |
| (sin fila) | `POST /decision` | (rechazado) | `decision` inválida → DEC_002; `no_go` sin motivo → DEC_002 |
| go | `POST /decision` (re-decidir) | no_go | Reemplaza la decisión (sin historial en esta fase) |
| no_go | `POST /decision` (re-decidir) | go | Reemplaza la decisión (sin historial en esta fase) |
| go / no_go | `GET /decision` | — | Devuelve el estado vigente |

---

## 5. REST Endpoints — `[Authorize]`

### POST /api/v1/licitaciones/{codigoExterno}/decision — Registrar GO/NO GO

**Description**: Registra la decisión formal del gerente. **Siempre humana**: el body solo
trae `{decision, motivo}`; la recomendación IA se **copia como snapshot** del último análisis
comercial completado de la licitación (si existe) al momento de decidir. Si la fila de interés
no existe, se crea (la decisión vive sobre `licitaciones_interes`, UK por licitación).

**Path Parameters**:
| Param | Type | Required | Description |
|-------|------|----------|-------------|
| `codigoExterno` | string | Yes | Código externo de la licitación (ej: `1425525-3-LE26`) |

**Request Body**:
```json
{
  "decision": "no_go",
  "motivo": "Requisitos técnicos exceden capacidades actuales y plazo de entrega es inviable"
}
```

**Response (200)** — decisión registrada (o reemplazada):
```json
{
  "success": true,
  "data": {
    "codigoExterno": "1425525-3-LE26",
    "decision": "no_go",
    "motivo": "Requisitos técnicos exceden capacidades actuales y plazo de entrega es inviable",
    "recomendacionIa": "no_go",
    "scoreConfianza": 0.84,
    "decididoPor": "gerente@tivit.cl",
    "decididoAt": "2026-08-16T16:20:00Z",
    "notificados": null
  }
}
```

**DB Object**: `usp_LicitacionesDecision_Registrar` (V144).
**Rules**:
- `decision` solo admite `go` / `no_go` (→ DEC_002).
- `motivo` obligatorio si `decision = no_go` (→ DEC_002); opcional si `go`.
- Snapshot: `recomendacion_ia` y `score_confianza` se copian de
  `analisis_licitacion_comercial.go_no_go` / `.score_confianza` del último análisis
  `completado`; si no hay análisis completado, quedan `NULL` (decisión sin respaldo IA).
- `decidido_por` = email del JWT del request; `decidido_at` = now.
- `notificados` queda `NULL` — se completa en Fase 3 (generador + avisos).
- Re-decidir reemplaza la decisión previa (sin historial en esta fase).
- Si el `codigoExterno` no existe → `LIC_001` (404).

**Error Codes**: `LIC_001` (404), `DEC_002` (422), `VAL_001` (400), `AUTH_001`, `SYS_001`.

### GET /api/v1/licitaciones/{codigoExterno}/decision — Estado de la decisión

**Description**: Estado de la decisión para la ficha de la licitación. No modifica nada.

**Path Parameters**:
| Param | Type | Required | Description |
|-------|------|----------|-------------|
| `codigoExterno` | string | Yes | Código externo de la licitación |

**Response (200)**:
```json
{
  "success": true,
  "data": {
    "decidida": true,
    "decision": "no_go",
    "motivo": "Requisitos técnicos exceden capacidades actuales...",
    "recomendacionIa": "no_go",
    "scoreConfianza": 0.84,
    "decididoPor": "gerente@tivit.cl",
    "decididoAt": "2026-08-16T16:20:00Z",
    "notificados": null
  }
}
```
Sin decisión: `{ "decidida": false, "decision": null, "motivo": null, ... }`.

**DB Object**: `usp_LicitacionesDecision_Obtener` (V144).
**Rules**: Requiere JWT. Lectura local (no consulta el análisis en vivo — el snapshot es la
verdad de la decisión).
**Error Codes**: `LIC_001` (404), `AUTH_001`.

---

## 6. Database Objects

| Endpoint | DB Object | Type | Params |
|----------|-----------|------|--------|
| POST /decision | `usp_LicitacionesDecision_Registrar` | Procedure (V144) | `p_licitacion_id`, `p_decision`, `p_motivo`, `p_recomendacion_ia`, `p_score_confianza`, `p_decidido_por`, `p_id` (OUT), `p_error_msg` (OUT) |
| GET /decision | `usp_LicitacionesDecision_Obtener` | Function (V144) | `p_licitacion_id` |
| POST /decision (snapshot) | `usp_AnalisisComercial_ObtenerUltimo` | Function (V142) | `p_licitacion_id` (lectura previa de la recomendación) |

**Migración**: `V144__Decisiones_GO_NO_GO.sql` (evolución de `licitaciones_interes` V122).

---

## 7. Shared DTOs

```json
{
  "DecisionRequest": {
    "decision": "string (go|no_go)",
    "motivo": "string?"
  },
  "DecisionDto": {
    "codigoExterno": "string",
    "decision": "string? (go|no_go)",
    "motivo": "string?",
    "recomendacionIa": "string? (strong_go|go|no_go|strong_no_go)",
    "scoreConfianza": "decimal? (0-1)",
    "decididoPor": "string?",
    "decididoAt": "datetime?",
    "notificados": "array<string>? (emails/ids — Fase 3)"
  }
}
```

---

## 8. Business Rules

### Validación
- `DEC-R001`: `decision` solo acepta `go` / `no_go` (case-insensitive, normalizado a minúsculas).
- `DEC-R002`: `motivo` **obligatorio en NO GO** (mín. 10 caracteres); opcional en GO.
- `DEC-R003`: `motivo` en GO no puede superar 4.000 caracteres.

### Decisión / snapshot
- `DEC-R004`: la decisión es **siempre humana** — la IA solo recomienda
  (`strong_go`…`strong_no_go`); el endpoint no acepta `recomendacion_ia` en el body.
- `DEC-R005`: al decidir se **copia el snapshot** de `recomendacion_ia` y `score_confianza`
  desde el último análisis completado (V142); re-análisis posteriores no alteran la decisión
  registrada.
- `DEC-R006`: sin análisis completado → snapshot `NULL` (decisión 100 % humana, permitida).
- `DEC-R007`: `decidido_por` proviene del JWT (nunca del body); `decidido_at` = servidor.
- `DEC-R008`: 1 fila por licitación (UK `licitacion_id`) — re-decidir **reemplaza** la
  decisión previa (sin historial en esta fase; si el negocio lo pide, Fase 3+ agrega tabla
  de historial).

### Ciclo de vida / cruce
- `DEC-R009`: `no_go` cierra el expediente comercial de la licitación (señal para la vista
  ejecutiva y para no ofertar); `go` habilita la generación de propuesta (Fase 3).
- `DEC-R010`: `notificados` se completa en **Fase 3** (avisos a personas marcadas a mano,
  GO y NO GO); hasta entonces queda `NULL`.
- `DEC-R011`: el registro crea la fila de interés si no existe (comportamiento del
  `usp_LicitacionesDecision_Registrar`), capturando el estado real de la licitación al marcar.

---

## 9. Error Codes

| Code | HTTP | Description | When |
|------|------|-------------|------|
| `DEC_001` | 404 | Licitación no encontrada | **Reutiliza `LIC_001`** (código inexistente) — DEC_001 es alias de contrato |
| `DEC_002` | 422 | Decisión inválida | `decision` ≠ `go`/`no_go`, o `no_go` sin `motivo` (o motivo < 10 chars) |
| `VAL_001` | 400 | Campo requerido o inválido | `decision` ausente en el body, `motivo` > 4.000 chars |
| `AUTH_001` | 401 | No autenticado | Token MPM faltante/expirado |
| `SYS_001` | 500 | Error interno | Error no manejado |

---

## Notas de consistencia (diseño vs. borrador técnico)

1. **`notificados`**: el diseño (§5 y §11.4) lo declaraba `varchar` ("emails/ids elegidos a
   mano"); V144 lo define **JSONB** (lista estructurada, mejor para Fase 3) → **la spec
   adopta JSONB** (spec manda).
2. **`notificado_at`**: el diseño §5 lo incluía; V144 no lo agrega. La spec no lo incluye
   (se define en Fase 3 con el envío real de avisos).
3. **`estado_licitacion_al_marcar = 1` hardcodeado** en `usp_LicitacionesDecision_Registrar`
   al crear la fila de interés: inconsistente con la spec 031 (V122), que captura el estado
   real al marcar. → La spec (DEC-R011) manda capturar el estado actual real de la
   licitación; si no está disponible, fallback 1. **Corregir en implementación.**
4. **Snapshot sin análisis**: el diseño asume que la decisión llega desde la vista ejecutiva
   (que ya tiene análisis + match). V144 copia los valores que el handler le pase; la spec
   define el comportamiento cuando no hay análisis (snapshot NULL, DEC-R006) — el diseño no
   lo explicitaba.
5. **Re-decisión**: el diseño no define historial; la UK `(licitacion_id)` de V122 obliga a
   reemplazo. La spec lo documenta como regla (DEC-R008) con nota de backlog para historial.
6. **DEC_001 vs LIC_001**: la instrucción de spec pide `DEC_001` "licitación no encontrada →
   reutiliza LIC_001". La spec lo modela como alias de contrato: el código de error emitido
   es `LIC_001` (consistente con el resto del repo) y `DEC_001` queda reservado como
   sinónimo documentado.
7. **Módulo físico**: la decisión vive en `MPM.Modules.Colaboracion` (no en Censo) — V144
   evoluciona la tabla V122 del módulo de Colaboración, consistente con D6.
