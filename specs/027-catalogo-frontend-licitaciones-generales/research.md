# Research: Frontend de Licitaciones Alineado al Catálogo Real

**Feature**: `027-catalogo-frontend-licitaciones-generales` | **Fecha**: 2026-07-16

## Reanálisis del sistema: qué ya existe

1. **`tipos_licitacion`** (`V045__Catalogo_Create_tipos_licitacion.sql`, seed en `V047`): tabla con `codigo SMALLINT PRIMARY KEY`, `nombre`, `slug`, `descripcion` — solo 4 filas (Licitación Pública/Trato Directo/Convenio Marco/Compra Ágil). Expuesta vía `usp_Catalogos_TiposLicitacion()` → `TipoLicitacionItemDto` (C#, `Codigo: int`) → `TipoLicitacionItem` (frontend, `codigo: number`).
2. **`estados_licitacion`**: `codigo SMALLINT PRIMARY KEY`, con FK real desde `licitaciones.codigo_estado`. Contiene 9 filas: los 5 códigos reales vigentes (5=Publicada, 6=Cerrada, 7=Desierta, 8=Adjudicada, 15=Revocada, fijados en `V086`) más 4 códigos heredados (1=Publicada, 2=Modificada, 3=Desierta, 4=Revocada) del catálogo original pre-V086.
3. **`licitaciones.tipo` NO tiene FK** hacia `tipos_licitacion` — es `VARCHAR(30)` libre. Confirmado por consulta directa: ya contiene 16 valores reales distintos (LE, LP, L, LR, LQ, O, CO, R, B, E, I, H, LS, CI, DC + el genérico "Licitacion" en 1 sola fila). El catálogo de tipos es puramente de referencia para UI, no impone integridad — se puede reemplazar su contenido sin migrar la tabla `licitaciones`.
4. **`licitaciones.codigo_estado` SÍ tiene FK**. De los 4 códigos heredados, **código 1 está activamente en uso por 144 licitaciones reales** (no es basura muerta): es el fallback intencional que la spec `026-robustez-sincronizacion-tipos-reales` (FR-007) definió para códigos de estado no estándar que devuelve la API. Los códigos 2, 3 y 4 tienen **0 filas** — sí son huérfanos sin uso real. Esto significa que **no se puede simplemente borrar el rango 1-4 del catálogo** sin romper el FK de esas 144 filas o sin re-mapear antes su estado.
5. **`alertas_reglas.tipos_licitacion`** ya es `TEXT[]` (`V079__Create_Alertas.sql`) — guarda códigos de tipo como texto libre, sin ninguna referencia a `tipos_licitacion.codigo`. Confirma que no hay ninguna otra tabla/módulo con una dependencia estructural sobre el tipo numérico actual del catálogo — el cambio de `tipos_licitacion.codigo` de `SMALLINT` a texto (para poder guardar "LE", "LP", etc.) no tiene blast radius fuera del módulo Catálogo y su consumo en frontend.
6. **Enriquecimiento en detalle ya existe**: `LicitacionService.ObtenerPorCodigoAsync` (`src/MPM.Modules.Licitaciones/Services/LicitacionService.cs:29-53`) ya consulta la API de detalle de Mercado Público bajo demanda cuando `Descripcion` y `FechaPublicacion` están vacíos, y persiste el resultado (`ActualizarDetalleAsync`). Esto significa que **FR-005 (Organismo/Monto/Items visibles en el detalle) ya está mayormente resuelto por código existente** — no hace falta construir un mecanismo nuevo, solo confirmar que sigue funcionando igual tras sacar esas columnas de la tabla/listado.

## Decisión 1: catálogo de Tipo — cambiar la clave de SMALLINT a texto

**Decisión**: `tipos_licitacion.codigo` pasa de `SMALLINT` a `VARCHAR(10)`, poblado con los códigos reales del glosario de la spec 026 (LE, LP, LQ, LR, CO, CA, TD, LS, L, B, R, E, I) más los 4 códigos nuevos observados en datos reales sin descripción documentada todavía (O, H, CI, DC — con `descripcion = 'Pendiente de documentar'`). `TipoLicitacionItemDto.Codigo` (C#) y `TipoLicitacionItem.codigo` (frontend) cambian de `int`/`number` a `string`.

**Rationale**: es el cambio mínimo que resuelve FR-001/FR-002 — sin FK real sobre `licitaciones.tipo`, no hay riesgo de romper datos existentes. El selector de Tipo del frontend ya arma sus opciones a partir de este catálogo (`LicitacionFilterBar.tsx:30-33`), así que una vez poblado correctamente el filtro funciona sin más cambios de lógica, solo actualizando qué campo se usa como `value` (el código real, no el `slug` genérico actual que no tiene relación con los valores reales de la columna `tipo`).

**Alternativas consideradas**: mantener `codigo` numérico y agregar una columna de texto aparte (`codigo_real`) — descartado por duplicar la clave sin necesidad, ya que nada más en el sistema depende del tipo numérico actual.

## Decisión 2: catálogo de Estado — filtrar en la consulta, no borrar filas

**Decisión**: `usp_Catalogos_EstadosLicitacion()` se modifica para devolver únicamente los 5 códigos reales vigentes (`WHERE codigo IN (5,6,7,8,15)`), sin tocar el contenido de la tabla `estados_licitacion` ni las 144 filas de `licitaciones` que hoy usan el código 1 como fallback.

**Rationale**: borrar o renumerar los códigos heredados del catálogo rompería el FK para las 144 licitaciones en código 1 (fallback intencional de la spec 026) — no es un dato basura, es un mecanismo ya diseñado a propósito. Filtrar en la función que alimenta el selector de UI resuelve el requisito visible (FR-003: 5 opciones sin duplicados) sin tocar datos ni arriesgar integridad referencial.

**Alternativas consideradas**: eliminar físicamente los códigos 2, 3, 4 (sin uso, se podría) y remapear las 144 filas del código 1 a alguno de los 5 reales — descartado para esta spec porque remapear el código 1 requeriría decidir a qué estado real corresponde cada una de esas 144 licitaciones, lo cual es trabajo de reconciliación de datos fuera del alcance acordado (ver spec `028-fix-estado-tipo-scraper-tivit`, parqueada) — no bloquea el objetivo de esta spec, que es el catálogo/frontend.

## Decisión 3: columnas Monto/Items/Organismo — quitar de la tabla, mantener en el detalle

**Decisión**: se remueven las columnas `Organismo`, `Monto` e `Items` de `LicitacionesTable.tsx` (tabla del listado). No se toca `LicitacionDetailDrawer.tsx` — sigue mostrando esos campos cuando existen, apoyado en el enriquecimiento bajo demanda ya existente (`ObtenerPorCodigoAsync`).

**Rationale**: es un cambio de columnas de tabla, no de datos por fila — no hace falta ninguna señal para "distinguir automáticamente" licitación general vs. de participación TIVIT a nivel de fila (lo que originalmente parecía necesitar FR-006). Al ser una decisión a nivel de columna de la tabla completa, se simplifica: ninguna fila muestra esas 3 columnas en el listado, y cualquier fila (general o de TIVIT) las sigue mostrando en su ficha de detalle si están disponibles. Esto satisface FR-004 y FR-005 sin construir lógica de detección de origen nueva.

**Nota sobre FR-006 del spec**: con esta decisión, el requisito de "distinguir automáticamente" queda resuelto de forma implícita (la distinción es simplemente "tabla vs. detalle", no "licitación A vs. licitación B") — no requiere una señal de origen de datos nueva ni tocar la pregunta de si una licitación es general o de TIVIT.

## Migración

Última migración aplicada al momento de este research: **V107** (`018-buscador-inteligente-nl`). La migración de esta feature sería **V108**: confirmar el número exacto contra el estado real de `src/MPM.Api/Database/Scripts/` al momento de implementar.
