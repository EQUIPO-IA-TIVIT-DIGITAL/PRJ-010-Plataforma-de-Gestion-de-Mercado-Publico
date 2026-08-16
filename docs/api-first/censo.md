# API Spec — Módulo Censo (CEN)

**Versión**: 1.0
**Módulo**: Censo (CEN) — `MPM.Modules.Censo`
**Generado por**: api-first-spec
**Fecha**: 2026-08-16
**Rama**: `036-flujo-comercial-ofertas`
**Diseño origen**: [docs/design/flujo-ofertas.md](../design/flujo-ofertas.md) (§11, D7.1–D7.12)
**HUs de origen**: pendientes

---

## 1. Scope

### Included
- Match de capacidades TIVIT contra la API de Census (personas, skills `levelSkill` 1-4,
  certificaciones) para una licitación: expansión de conceptos por capas (catálogo first,
  IA solo como fallback cacheado), consultas paralelas con semáforo (máx 8), cache por
  tecnología (TTL 24 h), dedup por email/corporateId, scoring de cobertura (x/total) con
  bonus por país de ejecución.
- Persistencia del resultado del match por licitación (`censo_match`) para el GET de estado.
- Catálogo local de types/tecnologías refrescable desde `census/knowledge` (`censo_catalogo`),
  con cache de expansión (`censo_expansiones`) y cache de personas por tecnología+país
  (`censo_cache_personas`).
- Preferencias por usuario: toggle "Filtrar por país" (OFF por defecto) + país
  (`censo_preferencias`), con override por licitación vía body del match.
- Gestión de token Census (`CensusTokenManager`): JWT `exp` con margen 2 min, renovación
  con `SemaphoreSlim`, retry ante 401.

### Excluded
- Decisión GO/NO GO formal → spec [decisiones.md](decisiones.md) (MPM.Modules.Colaboracion).
- Archivos de certificación (`certifications/file/{fileId}`) y sección "Certificaciones"
  de la propuesta → Fase 3.
- Búsqueda de experiencias profesionales (`professional-experience/*` → 401 con rol
  `service`, D7.6) → Fase 3 usa catálogo manual.
- Chat sobre el match → backlog.
- Réplica persistente de Census (índice local de `user-certifications`): descartada (D7.11).

---

## 2. Data Model

```mermaid
erDiagram
    censo_catalogo {
        bigint id PK
        varchar grupo "grupo de conocimiento Census"
        varchar categoria "categoría dentro del grupo"
        varchar type_name "type (concepto amplio: Front-END, Back-End)"
        varchar tecnologia "tecnología concreta (react, python)"
        timestamp created_at
    }
    censo_expansiones {
        bigint id PK
        varchar concepto UK "término de la licitación"
        jsonb tecnologias "tecnologías validadas"
        varchar fuente "catalogo|ia"
        timestamp created_at
        timestamp updated_at
    }
    censo_cache_personas {
        bigint id PK
        varchar tecnologia
        varchar pais "'' = todos los países"
        jsonb personas "respuesta cruda de Census"
        timestamp updated_at
    }
    censo_match {
        bigint id PK
        bigint licitacion_id FK UK
        jsonb resultado_json "resultado del match (DTO serializado)"
        timestamp created_at
        timestamp updated_at
    }
    censo_preferencias {
        bigint id PK
        varchar user_id UK "email del usuario"
        boolean filtrar_pais "OFF default"
        varchar pais "selector (default Chile)"
        timestamp created_at
        timestamp updated_at
    }
    censo_match }o--|| licitaciones : "licitacion_id"
```

### Table: censo_catalogo (V143)

| Column | Type | Nullable | Default | Description |
|--------|------|----------|---------|-------------|
| id | BIGSERIAL | NO | — | PK |
| grupo | VARCHAR(100) | NO | — | Grupo de conocimiento Census (KnowledgeGroup.name) |
| categoria | VARCHAR(150) | NO | — | Categoría (KnowledgeCategory.name) |
| type_name | VARCHAR(200) | NO | — | Type (KnowledgeType.name) — concepto amplio |
| tecnologia | VARCHAR(200) | NO | — | Tecnología (KnowledgeItem.name) |
| created_at | TIMESTAMP | NO | CURRENT_TIMESTAMP | Fila creada en el último refresh |

Indexes: PK `id` · UK `(type_name, tecnologia)` · IX `type_name` · IX `tecnologia`.

### Table: censo_expansiones (V143)

| Column | Type | Nullable | Default | Description |
|--------|------|----------|---------|-------------|
| id | BIGSERIAL | NO | — | PK |
| concepto | VARCHAR(200) | NO | — | Término a expandir (UK) |
| tecnologias | JSONB | NO | — | `["react", "next.js", ...]` validadas contra catálogo |
| fuente | VARCHAR(20) | NO | 'catalogo' | `catalogo` (capas 1-2, $0) o `ia` (fallback cacheado) |
| created_at / updated_at | TIMESTAMP | NO | CURRENT_TIMESTAMP | Auditoría del cache |

### Table: censo_cache_personas (V143)

| Column | Type | Nullable | Default | Description |
|--------|------|----------|---------|-------------|
| id | BIGSERIAL | NO | — | PK |
| tecnologia | VARCHAR(200) | NO | — | Tecnología consultada a Census |
| pais | VARCHAR(100) | NO | '' | `''` = todos los países (filtro OFF); nombre si filtro ON |
| personas | JSONB | NO | — | Respuesta cruda de `technologies/users` o `certifications/users` |
| updated_at | TIMESTAMP | NO | CURRENT_TIMESTAMP | Frescura: TTL 24 h |

Indexes: PK `id` · UK `(tecnologia, pais)` · IX `tecnologia`.

### Table: censo_match (V143)

| Column | Type | Nullable | Default | Description |
|--------|------|----------|---------|-------------|
| id | BIGSERIAL | NO | — | PK |
| licitacion_id | BIGINT | NO | — | FK → licitaciones(id), UK |
| resultado_json | JSONB | NO | — | `CensoMatchResultDto` serializado (último match) |
| created_at / updated_at | TIMESTAMP | NO | CURRENT_TIMESTAMP | Trazabilidad |

### Table: censo_preferencias (V143)

| Column | Type | Nullable | Default | Description |
|--------|------|----------|---------|-------------|
| id | BIGSERIAL | NO | — | PK |
| user_id | VARCHAR(200) | NO | — | Email del usuario (UK) |
| filtrar_pais | BOOLEAN | NO | FALSE | Toggle OFF por defecto (D7.12) |
| pais | VARCHAR(100) | NO | 'Chile' | País del selector (solo usado si filtrar_pais=true) |
| created_at / updated_at | TIMESTAMP | NO | CURRENT_TIMESTAMP | Auditoría |

---

## 3. Required Catalogs

### Enum: MatchEstado (application-level, no se almacena)

| Value | Description |
|-------|-------------|
| `no_ejecutado` | Aún no hay match para la licitación |
| `en_curso` | Match en ejecución (guard in-process, ventana ~3 s) |
| `completado` | Resultado persistido en `censo_match` |
| `error` | Falló (Census inalcanzable) — reintento vía POST |

### Enum: FuenteExpansion (application-level)

| Value | Description |
|-------|-------------|
| `catalogo` | Capa 1 (fuzzy types ≥80) o Capa 2 (fuzzy tecnología) — sin IA |
| `ia` | Capa 3 (LLM fallback) — se paga 1 vez por concepto y queda cacheado |

### Enum: Paises (application-level, selector de preferencia; no se almacena como catálogo)

| Value | Descripción |
|-------|-------------|
| Chile / Brasil / Perú / Colombia / Argentina / Ecuador / México / Otros | Países TIVIT para el filtro `workCountry` (filtro estricto en Census, D7.11) |

---

## 4. State Flow

### Match de capacidades (por licitación)

| Estado | Acción | Siguiente | Condiciones |
|--------|--------|-----------|-------------|
| (ninguno) | `POST /match-capacidades` | completado | Match síncrono OK (~3 s benchmark, D7.10) |
| (ninguno) | `POST /match-capacidades` | en_curso → error | Census inalcanzable tras retry 401 (CEN_002) |
| en_curso | (segundo POST simultáneo) | en_curso | Guard in-process → CEN_003 (409) |
| completado | `POST /match-capacidades` (nuevo body o cache fría) | completado | Re-ejecución reemplaza `censo_match` |
| completado | `GET /match-capacidades` | — | Devuelve el resultado persistido |

### Preferencias de usuario

| Estado | Acción | Siguiente |
|--------|--------|-----------|
| (sin fila) | `GET /usuarios/me/preferencias-censo` | Defaults (filtrarPais=false, pais=Chile) sin persistir |
| (sin fila) | `PUT /usuarios/me/preferencias-censo` | Fila creada (UPSERT) |
| (con fila) | `PUT ...` | Actualización parcial (UPSERT) |

---

## 5. REST Endpoints — `[Authorize]`

### POST /api/v1/licitaciones/{codigoExterno}/match-capacidades — Ejecutar match

**Description**: Ejecuta el match de capacidades contra Census usando los requisitos de la
licitación. El body es **opcional**: si llega vacío, los requisitos se toman del último
análisis comercial completado (`analisis_licitacion_comercial.resultado_json.requisitos` →
certificaciones + skills). Síncrono: el benchmark real (16 consultas, semáforo 8) fue
**3.044 ms** (D7.10); con cache por tecnología caliente ≈ 0 ms. Respeta el override por
licitación de país (body > preferencias > defaults).

**Path Parameters**:
| Param | Type | Required | Description |
|-------|------|----------|-------------|
| `codigoExterno` | string | Yes | Código externo de la licitación (ej: `1425525-3-LE26`) |

**Request Body (opcional)** — `CensoMatchRequest`:
```json
{
  "tecnologias": ["SIEM", "EDR"],
  "certificaciones": ["ISO 27001"],
  "filtrarPais": false,
  "pais": "Chile"
}
```
Todos los campos son opcionales (precedencia: body > preferencias del usuario > defaults).
Con `{}` o sin body → requisitos del análisis (si no hay análisis completado → `CEN_004`).

**Response (200)** — síncrono con resultado:
```json
{
  "success": true,
  "data": {
    "estado": "completado",
    "ejecutadoEn": "2026-08-16T15:04:11Z",
    "consultas": 16,
    "cacheUsadas": 9,
    "tecnologiasExpandidas": ["SIEM", "EDR", "SOAR", "SOC"],
    "personas": [
      {
        "nombre": "María González",
        "email": "maria.gonzalez@tivit.com",
        "corporateId": "TIV-1234",
        "pais": "Chile",
        "cargo": "Security Analyst",
        "cobertura": 4,
        "totalRequeridos": 5,
        "skills": ["SIEM", "SOC", "EDR"],
        "certificaciones": ["ISO 27001"]
      }
    ],
    "resumen": {
      "totalPersonas": 92,
      "maxCobertura": 5,
      "personasConCoberturaAlta": 2
    }
  }
}
```

**DB Objects**: `usp_CensoMatch_Guardar` (persiste), `usp_CensoCachePersonas_ObtenerFresco` /
`usp_CensoCachePersonas_Upsert` (cache por tecnología), `usp_CensoExpansion_Obtener` /
`usp_CensoExpansion_Upsert` (expansión cacheada), `usp_CensoCatalogo_Listar` (capas 1-2).

**Rules**:
- Expansión por capas: Capa 1 fuzzy types ≥80 (22,6 ms, $0) → Capa 2 fuzzy tecnología →
  Capa 3 IA fallback persistida en `censo_expansiones` (una vez por concepto, nunca re-paga).
- Consultas Census en paralelo con **semáforo máx 8**; cache por tecnología+país TTL 24 h.
- Dedup por `email`/`corporateId`; scoring = cobertura (skills matcheados/total) + levelSkill
  (1-4) + bonus por país de ejecución cuando el filtro está OFF (no excluye a nadie).
- Mandatorias primero (descartan), deseables después.
- Token gestionado por `CensusTokenManager`: exp JWT − 2 min de margen, renovación única
  concurrente, retry 1 vez ante 401.
- `filtrarPais=true` → `workCountry` acota la consulta; `false`/ausente → se omite (match LATAM).

**Error Codes**: `LIC_001` (404), `CEN_001` (422), `CEN_002` (502), `CEN_003` (409),
`CEN_004` (422), `AUTH_001`, `SYS_001`.

### GET /api/v1/licitaciones/{codigoExterno}/match-capacidades — Estado + resultado

**Description**: Estado y resultado del último match de la licitación (para la ficha y el
polling tras un POST). No consulta Census: lee `censo_match`.

**Path Parameters**:
| Param | Type | Required | Description |
|-------|------|----------|-------------|
| `codigoExterno` | string | Yes | Código externo de la licitación |

**Response (200)**:
```json
{
  "success": true,
  "data": {
    "estado": "completado",
    "ultimoEjecutadoAt": "2026-08-16T15:04:11Z",
    "match": {
      "ejecutadoEn": "2026-08-16T15:04:11Z",
      "consultas": 16,
      "cacheUsadas": 9,
      "tecnologiasExpandidas": ["SIEM", "EDR"],
      "personas": [],
      "resumen": { "totalPersonas": 92, "maxCobertura": 5, "personasConCoberturaAlta": 2 }
    }
  }
}
```
`estado: "no_ejecutado"` y `match: null` cuando la licitación aún no tiene match.

**DB Object**: `usp_CensoMatch_Obtener`.
**Rules**: Requiere JWT. No dispara ninguna consulta externa (solo lectura local).
**Error Codes**: `LIC_001` (404), `AUTH_001`.

### GET /api/v1/censo/catalogo — Catálogo de types/tecnologías

**Description**: Catálogo local (grupo → categoría → type → tecnología) para autocompletar
en la UI. Dataset pequeño (~1.2 k filas: 210 types, ~939 tecnologías) → sin paginación.

**Query Parameters**:
| Param | Type | Required | Default | Description |
|-------|------|----------|---------|-------------|
| `q` | string | No | — | Filtro case-insensitive por type o tecnología (min 2 chars) |
| `grupo` | string | No | — | Filtro por grupo |
| `categoria` | string | No | — | Filtro por categoría |

**Response (200)**:
```json
{
  "success": true,
  "data": {
    "items": [
      { "grupo": "Desenvolvimento", "categoria": "Front-End", "typeName": "Front-END", "tecnologia": "React" }
    ],
    "resumen": { "types": 210, "tecnologias": 939, "actualizadoAt": "2026-08-16T14:00:00Z" }
  }
}
```

**DB Object**: `usp_CensoCatalogo_Listar` (retorna todo; el filtrado `q`/`grupo`/`categoria`
se aplica en el servicio sobre el listado — volumen pequeño, sin SQL dinámico).
**Rules**: Si el catálogo está vacío → refresco lazy automático desde `census/knowledge`
(vía `POST /catalogo/refrescar`) antes de responder.
**Error Codes**: `AUTH_001`, `SYS_001`.

### POST /api/v1/censo/catalogo/refrescar — Refrescar catálogo desde Census

**Description**: Refresca `censo_catalogo` desde `GET /census/knowledge` (59 KB, verificado
200 con rol service, D7.3/D7.7): limpia (truncate) y reinserta grupo→categoría→type→tecnología.
Método manual (SuperAdmin) + refresco lazy automático si el catálogo está vacío.

**Request Body**: Ninguno.

**Response (200)**:
```json
{
  "success": true,
  "data": {
    "grupos": 8,
    "categorias": 24,
    "types": 210,
    "tecnologias": 939,
    "durationMs": 1840
  }
}
```

**DB Objects**: `usp_CensoCatalogo_Limpiar` + `usp_CensoCatalogo_Upsert` (batch), vía
`CensoCatalogoService` + `CensusClient` (token del manager).
**Rules**: Requiere rol con permiso de administración del módulo Censo (no cualquier JWT).
La expansión cacheada (`censo_expansiones`) NO se invalida en el refresh (sigue siendo válida;
las tecnologías nuevas se resuelven con el catálogo actualizado en la próxima expansión).
**Error Codes**: `CEN_002` (502 — Census inalcanzable), `AUTH_001` (401/403), `SYS_001`.

### GET /api/v1/usuarios/me/preferencias-censo — Preferencias del usuario

**Description**: Toggle "Filtrar por país" + país (D7.12). Si no hay fila → defaults
(filtrarPais=false, pais="Chile") sin persistir.

**Response (200)**:
```json
{
  "success": true,
  "data": { "filtrarPais": false, "pais": "Chile" }
}
```

**DB Object**: `usp_CensoPreferencias_Obtener` (si no hay fila → defaults).
**Rules**: `user_id` se obtiene del JWT (claim de email).
**Error Codes**: `AUTH_001`, `SYS_001`.

### PUT /api/v1/usuarios/me/preferencias-censo — Actualizar preferencias

**Description**: Actualización parcial (UPSERT) del toggle y/o país. `filtrarPais=true`
requiere `pais` no vacío.

**Request Body** — `CensoPreferenciasUpdateDto`:
```json
{ "filtrarPais": true, "pais": "Chile" }
```

**Response (200)**:
```json
{ "success": true, "data": { "filtrarPais": true, "pais": "Chile" } }
```

**DB Object**: `usp_CensoPreferencias_Upsert`.
**Rules**: El override por licitación (body del match) no modifica la preferencia.
**Error Codes**: `VAL_001` (400 — `pais` vacío con filtro ON), `AUTH_001`, `SYS_001`.

---

## 6. Database Objects

| Endpoint | DB Object | Type | Params |
|----------|-----------|------|--------|
| POST /match-capacidades | `usp_CensoMatch_Guardar` | Procedure (V143) | `p_licitacion_id`, `p_resultado_json`, `p_error_msg` |
| POST /match-capacidades | `usp_CensoExpansion_Obtener` / `_Upsert` | Function / Procedure | `p_concepto` · `p_concepto`, `p_tecnologias`, `p_fuente` |
| POST /match-capacidades | `usp_CensoCachePersonas_ObtenerFresco` / `_Upsert` | Function / Procedure | `p_tecnologia`, `p_pais` · + `p_personas` |
| POST /match-capacidades | `usp_CensoCatalogo_Listar` | Function | — (capas 1-2) |
| GET /match-capacidades | `usp_CensoMatch_Obtener` | Function | `p_licitacion_id` |
| GET /censo/catalogo | `usp_CensoCatalogo_Listar` | Function | — (filtros en servicio) |
| POST /censo/catalogo/refrescar | `usp_CensoCatalogo_Limpiar` + `usp_CensoCatalogo_Upsert` | Procedure ×N | — · `p_grupo`, `p_categoria`, `p_type_name`, `p_tecnologia` |
| GET /usuarios/me/preferencias-censo | `usp_CensoPreferencias_Obtener` | Function | `p_user_id` |
| PUT /usuarios/me/preferencias-censo | `usp_CensoPreferencias_Upsert` | Procedure | `p_user_id`, `p_filtrar_pais`, `p_pais` |

**Migración**: `V143__Censo.sql`.

---

## 7. Shared DTOs

```json
{
  "CensoMatchRequest": {
    "tecnologias": ["string?"], "certificaciones": ["string?"],
    "filtrarPais": "bool?", "pais": "string?"
  },
  "CensoMatchResultDto": {
    "ejecutadoEn": "datetime", "consultas": "int", "cacheUsadas": "int",
    "tecnologiasExpandidas": ["string"], "personas": ["CensoPersonaDto"],
    "resumen": "CensoResumenDto"
  },
  "CensoPersonaDto": {
    "nombre": "string", "email": "string", "corporateId": "string", "pais": "string",
    "cargo": "string", "cobertura": "int", "totalRequeridos": "int",
    "skills": ["string"], "certificaciones": ["string"]
  },
  "CensoResumenDto": {
    "totalPersonas": "int", "maxCobertura": "int", "personasConCoberturaAlta": "int"
  },
  "CensoCatalogoItemDto": {
    "grupo": "string", "categoria": "string", "typeName": "string", "tecnologia": "string"
  },
  "CensoPreferenciasDto": { "filtrarPais": "bool", "pais": "string" },
  "CensoPreferenciasUpdateDto": { "filtrarPais": "bool?", "pais": "string?" }
}
```

**DTOs externos de Census (no se serializan como contrato MPM — se mapean internamente)**:
- `POST /external-auth/token` → `{ accessToken, securityToken }`
- `GET /services/knowledge/technologies/users?technologyName&workCountry` →
  `UserTechnologyResponse { userName, userEmail, workCountry, corporateId, functionFullName, technologies: [{ name, levelSkill }] }`
- `GET /services/knowledge/certifications/users?certificationName&workCountry` →
  `SimpleUserCertificationResponse { userName, userEmail, workCountry, corporateId, functionFullName, certifications: [...] }`
- `GET /census/knowledge` → `KnowledgeGroup { name, categories: [{ name, types: [{ name, knowledge: [{ name, level }] }] }] }`

---

## 8. Business Rules

### Validación
- `CEN-R001`: la cobertura **parcial es válida** — se incluye toda persona con ≥1 skill o
  certificación matcheada y se muestra `cobertura/totalRequeridos` (7/10 > 3/10; el scoring
  ordena, no descarta).
- `CEN-R002`: si el body del match está vacío y no hay análisis comercial completado
  (`analisis_licitacion_comercial` con estado `completado`) → `CEN_004`.
- `CEN-R003`: si el body trae listas vacías (`tecnologias: []`, `certificaciones: []`) y el
  análisis no aporta requisitos → `CEN_001`.

### Match / expansión
- `CEN-R004`: expansión por capas — Capa 1 fuzzy types ≥80 (sin IA), Capa 2 fuzzy tecnología,
  Capa 3 IA fallback **persistida en `censo_expansiones`** (1 vez por concepto; nunca re-paga).
- `CEN-R005`: consultas Census en paralelo con **semáforo máx 8 concurrentes**; tiempo
  objetivo < 10 s (benchmark 3.044 ms con 16 consultas).
- `CEN-R006`: **cache por tecnología+país TTL 24 h** (`censo_cache_personas`) — la 2ª
  licitación con los mismos skills no consulta Census (≈0 ms con cache caliente).
- `CEN-R007`: dedup por `email`/`corporateId` al unir resultados de múltiples consultas.
- `CEN-R008`: priorización mandatorias (skills/certificaciones que descartan) antes que
  deseables; las personas con cobertura en mandatorias rankean primero.
- `CEN-R009`: scoring = cobertura (matcheados/total) + levelSkill (1-4) + **bonus por país
  de ejecución** cuando el filtro está OFF (no excluye a nadie, solo rankea — D7.12).
- `CEN-R010`: filtro de país OFF por defecto; ON acota con `workCountry` (filtro estricto de
  Census, D7.11). Precedencia: body del match > preferencias del usuario > defaults.

### Token / integración
- `CEN-R011`: `CensusTokenManager` — expiración derivada del JWT `exp` con **margen 2 min**
  (renovar antes); sin `exp` → TTL conservador (default 4 min); renovación única concurrente
  (`SemaphoreSlim`); **retry 1 vez ante 401** (token invalidado prematuramente — BUG-023);
  `securityToken` se renueva junto.
- `CEN-R012`: **cero réplica persistente de Census** — solo cache de resultados
  (`censo_cache_personas`, TTL 24 h) y expansiones (`censo_expansiones`); nunca se copia
  `user-certifications` (D7.11).
- `CEN-R013`: las credenciales de servicio Census viven en configuración/secretos
  (`config_census`), nunca en el repositorio (D7.1).
- `CEN-R014`: la vista ejecutiva combina resumen del análisis IA + match de capacidades +
  recomendación → el gerente decide (la decisión vive en decisiones.md).

---

## 9. Error Codes

| Code | HTTP | Description | When |
|------|------|-------------|------|
| `LIC_001` | 404 | Licitación no encontrada | `codigoExterno` inexistente |
| `CEN_001` | 422 | Sin requisitos para el match | Body vacío y análisis sin requisitos extraíbles |
| `CEN_002` | 502 | Census inalcanzable | Fallo de red/auth persistente (tras retry 401) en match o refresco de catálogo |
| `CEN_003` | 409 | Match en curso | Segundo POST simultáneo para la misma licitación (guard in-process) |
| `CEN_004` | 422 | Sin documentos analizados | Body vacío y no hay análisis comercial completado (Fase 1) |
| `VAL_001` | 400 | Campo requerido o inválido | `pais` vacío con filtro ON, `q` < 2 chars |
| `AUTH_001` | 401 | No autenticado | Token MPM faltante/expirado |
| `SYS_001` | 500 | Error interno | Error no manejado |

---

## Notas de consistencia (diseño vs. borrador técnico)

1. **`censo_match` y `censo_preferencias` no figuran en §11.4 del diseño**, pero son
   requeridos por los endpoints de §11.3 (GET match → persistencia; preferencias → toggle
   por usuario). El borrador V143 ya las incluye → **la spec las adopta** (spec manda).
2. **`POST /api/v1/censo/catalogo/refrescar` no está listado en §11.3** del diseño. Se deriva
   de `CensoCatalogoService` ("catálogo refrescable desde `census/knowledge`", D7.7/D7.8) y
   es requerido explícitamente por esta spec → se incluye con refresco manual + lazy.
3. **Nombre de tabla**: D7.9 menciona `censo_cache_personas_tecnologia`; §11.4 y V143 usan
   `censo_cache_personas` → la spec adopta `censo_cache_personas` (consistente con V143).
4. **Sincronía vs. 202+polling**: §11.3 describía "202 + polling si es primera vez, 200 con
   cache". El benchmark D7.10 (16 consultas = 3.044 ms) habilita el **síncrono** que esta
   spec fija (y la instrucción de spec lo ordena). Si el match superara ~10 s, se puede
   volver a 202+polling sin cambiar el contrato: el GET ya devuelve estado.
5. **V143 no tiene SP de "match en curso"**: `CEN_003` se implementa con guard in-process
   (sin columna de estado en `censo_match`) — el draft no lo contempla; la spec lo define.
6. **`usp_CensoCatalogo_Listar` no recibe filtros**: el endpoint GET /censo/catalogo acepta
   `q`/`grupo`/`categoria` pero el filtrado se hace en el servicio sobre el listado completo
   (~1.2 k filas) — sin SQL dinámico. Aceptable por volumen; si crece, migrar a SP con filtros.
7. **`censo_preferencias.pais` default 'Chile'** (V143) es solo el valor inicial del selector;
   el bonus por país de ejecución (CEN-R009) se calcula con el país sugerido por el análisis,
   no con esta preferencia.
8. **`censo_cache_personas` guarda la respuesta cruda de Census** (JSONB) sin normalizar;
   la normalización (dedup, scoring) ocurre en `CensoMatchService` al leer el cache o la
   respuesta fresca. El borrador del handler es consistente con esto.
