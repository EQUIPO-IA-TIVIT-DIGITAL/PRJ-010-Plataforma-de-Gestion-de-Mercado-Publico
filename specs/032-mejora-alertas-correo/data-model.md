# Data Model: Mejora de Alertas por Correo

## Entidades afectadas

### `LicitacionParaMatching` (record, `MPM.Modules.Alertas.Models.AlertasDtos`)

Proyección de una licitación usada exclusivamente durante el ciclo de matching de alertas. Se amplía con 2 campos nuevos, ambos de solo lectura desde columnas ya existentes en `licitaciones`:

| Campo | Tipo | Origen | Estado |
|---|---|---|---|
| `LicitacionId` | `long` | `licitaciones.id` | Existente |
| `CodigoExterno` | `string` | `licitaciones.codigo_externo` | Existente |
| `Nombre` | `string` | `licitaciones.nombre` | Existente |
| `Descripcion` | `string?` | `licitaciones.descripcion` | Existente |
| `Monto` | `decimal?` | `licitaciones.monto_estimado` | Existente |
| `TipoLicitacion` | `string?` | `licitaciones.tipo` | Existente |
| `Organismo` | `string?` | `licitaciones.organismo` | Existente |
| `FechaCierre` | `DateTime?` | `licitaciones.fecha_cierre` | **Nuevo** |
| `Link` | `string?` | `licitaciones.link` | **Nuevo** |

No se agrega ninguna columna nueva a la tabla `licitaciones` — `fecha_cierre` y `link` ya existen desde `V002__Create_licitaciones.sql`. El cambio es exclusivamente ampliar el `SELECT` de `usp_Licitaciones_ListarParaMatching` y el mapeo en `LicitacionHandler.ListarParaMatchingAsync`/`MatchingRow`.

### `EvaluarMatch` (método estático, `AlertasMatchingService`)

Sin cambios de firma ni de entidad — cambia únicamente el algoritmo de comparación interno (de `Contains` a `Regex.IsMatch` con límites de palabra), documentado en research.md R1.

### `EmailNotificationService.EnviarAsync`

Firma ampliada para recibir los 2 campos nuevos como parámetros opcionales, junto con los ya existentes (`toEmail`, `keyword`, `nombreLicitacion`, `codigoExterno`, `presupuesto`):

| Parámetro | Tipo | Nuevo/Existente |
|---|---|---|
| `toEmail` | `string` | Existente |
| `keyword` | `string` | Existente |
| `nombreLicitacion` | `string` | Existente |
| `codigoExterno` | `string` | Existente |
| `presupuesto` | `string?` | Existente |
| `organismo` | `string?` | **Nuevo** |
| `fechaCierre` | `DateTime?` | **Nuevo** |
| `link` | `string?` | **Nuevo** |

Cada campo nuevo se omite del cuerpo HTML del correo cuando es `null`/vacío (FR-006) — mismo patrón condicional que ya usa `presupuesto` hoy (línea 24 de `EmailNotificationService.cs`).

## Configuración de infraestructura (no es una entidad de datos, pero forma parte del modelo de esta mejora)

### Cloud Scheduler `sync-job-scheduler`

| Campo | Valor actual | Valor nuevo |
|---|---|---|
| `schedule` | `0 3,15 * * *` | `0 8,15 * * *` |
| `time_zone` | America/Santiago (sin cambios) | America/Santiago |
| Resto de la configuración (target, body, headers, etc.) | Sin cambios | Sin cambios |
