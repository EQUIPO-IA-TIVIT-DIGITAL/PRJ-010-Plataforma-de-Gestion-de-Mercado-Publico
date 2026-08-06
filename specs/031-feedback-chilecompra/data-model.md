# Data Model: Feedback ChileCompra (031)

Todas las tablas nuevas siguen la Constitución (Principio III: migraciones `.sql` embebidas en `src/MPM.Api/Database/Scripts/`, `VXXX__Descripcion.sql`, sin ORM). Siguiente número libre: **V118** (confirmar contra el estado real del directorio al implementar — no asumir contigüidad, ver `research.md` de otras specs que ya encontraron huecos en la numeración).

## Tablas nuevas

### `areas_negocio` (V118, módulo `MPM.Modules.Catalogo`)

| Columna | Tipo | Notas |
|---|---|---|
| `codigo` | SMALLINT PK | 1=Cloud, 2=Ciberseguridad, 3=Digital (semilla inicial, ver Assumptions de `spec.md`) |
| `nombre` | VARCHAR(50) | |
| `palabras_clave` | TEXT[] | términos usados en el `EXISTS` léxico contra `licitaciones.search_vector` (ver `research.md` §1) |
| `created_at` / `updated_at` | TIMESTAMP | |

Sin FK entrante desde `licitaciones` — la relación es calculada en consulta, no almacenada (decisión de `research.md` §1).

### `licitaciones_interes` (V122, módulo nuevo `MPM.Modules.Colaboracion`)

| Columna | Tipo | Notas |
|---|---|---|
| `id` | BIGINT GENERATED ALWAYS AS IDENTITY PK | |
| `licitacion_id` | BIGINT NOT NULL REFERENCES licitaciones(id) | **UNIQUE** — garantiza a nivel de esquema que solo existe un "interés" (y por lo tanto un solo análisis bajo demanda) por licitación, ver FR-013 |
| `workspace_id` | BIGINT NULL REFERENCES analisis_workspaces(id) | se completa cuando el análisis termina de generarse; nulo mientras está "generando" |
| `conversacion_id` | BIGINT NULL REFERENCES conversaciones(id) | se completa al crear la conversación grupal asociada |
| `marcado_por` | VARCHAR(100) NOT NULL | `user_id` de quien marcó la licitación de interés (mismo tipo que `mensajes.user_id`, no FK tipada — patrón ya inconsistente en el resto del sistema, ver `research.md` de la auditoría) |
| `estado_licitacion_al_marcar` | SMALLINT NOT NULL | copia de `licitaciones.codigo_estado` al momento de marcar, para poder señalar el cambio de estado (FR-017) comparando contra el estado actual en consulta |
| `created_at` / `updated_at` | TIMESTAMP | |

### `competidores_actividad_mercado` (V121, módulo `MPM.Modules.Competidores`)

Mismo patrón de cache que `competidores_analisis` (V098).

| Columna | Tipo | Notas |
|---|---|---|
| `id` | BIGINT GENERATED ALWAYS AS IDENTITY PK | |
| `nombre_competidor` | VARCHAR(300) NOT NULL | |
| `area_codigo` | SMALLINT NULL REFERENCES areas_negocio(codigo) | acota el scraping, ver `research.md` §4 |
| `fecha_desde` / `fecha_hasta` | DATE NOT NULL | mismo rango que el informe ejecutivo que lo solicitó |
| `estado` | VARCHAR(20) NOT NULL DEFAULT 'generando' | `generando` \| `listo` \| `error` (mismo vocabulario que `analisis_workspaces.estado`) |
| `cantidad_licitaciones` | INT NULL | total de licitaciones del competidor en el período/área, se completa al terminar |
| `monto_total_adjudicado` | NUMERIC(18,2) NULL | |
| `contenido_json` | JSONB NULL | detalle (lista de licitaciones, montos) para el informe ejecutivo ampliado |
| `generado_at` | TIMESTAMP NULL | |
| `created_at` / `updated_at` | TIMESTAMP | |
| — | UNIQUE(`nombre_competidor`, `area_codigo`, `fecha_desde`, `fecha_hasta`) | misma clave de cache que `competidores_analisis` |

## Tablas existentes reutilizadas (sin cambio de esquema, salvo lo indicado)

- **`licitaciones`** (V002): sin ALTER. La clasificación por área se calcula en consulta contra `search_vector` (V066), no se persiste.
- **`estados_licitacion`** (V001/V035/V086): sin cambio — es la fuente de los 5 estados reales (5=Publicada, 6=Cerrada, 7=Desierta, 8=Adjudicada, 15=Revocada) usados en US2.
- **`analisis_workspaces`** (V051): sin cambio de esquema. `usp_AnalisisWorkspaces_Listar` (V113) se reescribe en **V120** para proyectar y ordenar por `licitaciones.fecha_adjudicacion` (ver `research.md` §3).
- **`licitaciones_ofertas`** (V097) / `competidores_analisis` (V098): sin cambio — siguen representando "encuentros directos" (licitaciones donde TIVIT participó). La actividad total de mercado (US4) es un dato **adicional**, no un reemplazo.
- **`conversaciones`** (V013), **`conversacion_participantes`** (V014), **`mensajes`** (V015): sin cambio de esquema — reutilizadas tal cual para asignación + comentarios (US5, ver `research.md` §5). `conversaciones.licitacion_id` (ya existe, nunca usado) es exactamente el campo que conecta ambos mundos.

## Stored procedures nuevos o modificados

| SP | Migración | Módulo | Descripción |
|---|---|---|---|
| `usp_Catalogos_AreasNegocio()` | V118 | Catalogo | lista `areas_negocio` |
| `usp_Licitaciones_Listar` (rewrite) | V119 | Licitaciones | agrega `p_area SMALLINT`, `p_sin_clasificar BOOLEAN` a la firma existente (ver contrato en `contracts/`) |
| `usp_Licitaciones_ContarPorEstado(p_area, p_sin_clasificar)` | V119 | Licitaciones | nuevo — estadística por estado (US2) |
| `usp_AnalisisWorkspaces_Listar` (rewrite) | V120 | Analisis | agrega `fecha_adjudicacion` a la proyección, cambia el `ORDER BY` (US3) |
| `usp_CompetidoresActividadMercado_ObtenerCache` / `_Guardar` | V121 | Competidores | get-or-generate del cache de actividad total (US4) |
| `usp_LicitacionesInteres_Marcar` / `_Listar` / `_VincularWorkspace` / `_VincularConversacion` | V122 | Colaboracion (nuevo) | ciclo de vida de "licitación de interés" (US5) |

## Módulo nuevo: `MPM.Modules.Colaboracion`

Justificado por Principio I de la Constitución (dominio propio: "seguimiento colaborativo de licitaciones de interés", distinto de Licitaciones/Analisis/Mensajeria aunque los conecte). Estructura estándar:

```text
MPM.Modules.Colaboracion/
  Controllers/LicitacionesInteresController.cs
  Services/LicitacionesInteresService.cs
  Data/LicitacionesInteresHandler.cs + LicitacionesInteresStoredProcedures.cs
  Models/LicitacionInteresDto.cs
  ModuleRegistration.cs
```

No referencia a `MPM.Modules.Analisis` ni `MPM.Modules.Mensajeria` en C# — solo persiste los IDs que el frontend le pasa después de llamar a los endpoints existentes de esos módulos (ver `research.md` §5 y `contracts/colaboracion-interes.md`).
