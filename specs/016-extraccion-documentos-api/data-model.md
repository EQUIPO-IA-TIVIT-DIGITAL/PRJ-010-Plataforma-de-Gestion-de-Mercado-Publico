# Data Model: Extracción de Documentos vía API Directa

**Feature**: 016-extraccion-documentos-api | **Date**: 2026-07-01

Reutiliza tablas existentes (`licitaciones_adjuntos`, `scraper_sync_log` de V062). Una migración nueva (**V077**): 1 columna nueva + 1 tabla de registro por intento + SPs.

## 1. `licitaciones_adjuntos` (existente) — columna nueva

| Columna nueva | Tipo | Uso |
|---------------|------|-----|
| `metodo_extraccion` | `VARCHAR(20) DEFAULT 'navegador'` | Cómo se obtuvo el documento: `directo` (HTTP) o `navegador` (Playwright fallback). Permite auditar y comparar (US3). |

El resto de la tabla no cambia: `licitacion_id`, `tipo`, `nombre_archivo`, `ruta_storage`, `acta_descargada`, etc.

## 2. `extraccion_documentos_log` (tabla nueva)

Registro por intento de extracción de una licitación — habilita FR-005 (registro consultable de fallos) y FR-008/US3 (comparación en paralelo).

| Columna | Tipo | Descripción |
|---------|------|-------------|
| `id` | `BIGSERIAL PK` | |
| `licitacion_id` | `BIGINT` (FK `licitaciones`) | Licitación procesada |
| `codigo_externo` | `VARCHAR(50)` | Código MP (para consulta directa) |
| `metodo` | `VARCHAR(20)` | `directo` \| `navegador` |
| `estado` | `VARCHAR(20)` | `exito` \| `fallo` \| `sin_adjuntos` |
| `documentos_obtenidos` | `INT DEFAULT 0` | Cantidad de documentos descargados |
| `acta_obtenida` | `BOOLEAN DEFAULT FALSE` | Si se obtuvo el Acta de Evaluación |
| `error` | `TEXT` | Motivo del fallo (null si éxito) |
| `duracion_ms` | `BIGINT` | Tiempo del intento (para SC-001) |
| `es_fallback` | `BOOLEAN DEFAULT FALSE` | `true` si este intento fue el respaldo tras fallar el directo |
| `created_at` | `TIMESTAMP DEFAULT now()` | |

**Índices**: `(codigo_externo)`, `(estado)`, `(metodo, created_at)`.

**Reglas de estado**:

| estado | Condición |
|--------|-----------|
| `exito` | Se obtuvieron los documentos esperados (incluye el Acta cuando existe) |
| `sin_adjuntos` | La licitación no tiene adjuntos publicados (caso legítimo, NO es fallo) |
| `fallo` | No se pudieron obtener los documentos por este método |

**Fallo real de una licitación** (FR-006): existe un registro `directo/fallo` **y** un registro `navegador/fallo` (`es_fallback=true`) para la misma licitación → requiere atención.

## 3. Stored procedures nuevos (V077)

| SP | Parámetros | Retorno |
|----|-----------|---------|
| `usp_ExtraccionLog_Registrar` | `p_licitacion_id, p_codigo_externo, p_metodo, p_estado, p_documentos, p_acta, p_error, p_duracion_ms, p_es_fallback` | id del registro |
| `usp_ExtraccionLog_ResumenPeriodo` | `p_desde, p_hasta` | Conteos por método/estado (para comparación US3) |
| `usp_Adjuntos_ExistePorLicitacion` | `p_licitacion_id` | bool (idempotencia, R7) |

`usp_Adjuntos_Upsert` (si no existe uno equivalente del scraper) o el existente se reutiliza para persistir cada adjunto con `metodo_extraccion`.

## 4. Sesión MP (cache — sin BD)

| Elemento | Almacenamiento | Detalle |
|----------|----------------|---------|
| Cookies de sesión MP | **Redis** | Clave `mp:session:cookies`, valor = storageState JSON, TTL `Extraccion:SesionTtlHoras` (default 6h) |
| Bandera de renovación en curso | Redis (lock) | Evita múltiples logins simultáneos |

## 5. Configuración (appsettings / env — sin BD)

| Clave | Default | Uso |
|-------|---------|-----|
| `Extraccion:Modo` | `solo_navegador` | `solo_navegador` \| `paralelo` \| `directo_con_fallback` (R5) |
| `Extraccion:SesionTtlHoras` | `6` | Vigencia de las cookies cacheadas |
| `Extraccion:MaxConcurrencia` | `2` | Descargas simultáneas (anti-bot, R6) |
| `Extraccion:DelayMs` | `1500` | Espera entre licitaciones |

## 6. Entidades del dominio (mapa al spec)

- **Licitación** → `licitaciones` (existente).
- **Documento Adjunto** → `licitaciones_adjuntos` (existente + `metodo_extraccion`).
- **Registro de Extracción** → `extraccion_documentos_log` (nueva).
