# Data Model: Buscador Inteligente en Lenguaje Natural

**Feature**: `018-buscador-inteligente-nl`

Sin cambios de esquema de base de datos — no se agregan tablas ni columnas. Los "modelos" de esta feature son DTOs de request/response en `MPM.Modules.Licitaciones`.

## Consulta de búsqueda (request)

Entrada del usuario a `GET /api/v1/licitaciones/buscar-natural`.

| Campo | Tipo | Origen | Notas |
|---|---|---|---|
| `q` | string | Input del usuario | Texto libre en lenguaje natural, ya validado (`Trim().Length >= 2`) en `LicitacionService` |
| `page`, `pageSize` | int | Query params existentes | Sin cambios |
| `estado` | smallint? | Query param existente, ahora también inferible | Si el usuario lo pasa explícito, tiene prioridad sobre lo que infiera Gemini de `q` |

## Interpretación de consulta (interno, no persistido)

Salida de `ConsultaSemanticaService.InterpretarAsync(q)` — no se guarda en base de datos, vive solo en memoria durante el request.

| Campo | Tipo | Descripción |
|---|---|---|
| `TerminosExpandidos` | `List<string>` | Sinónimos/conceptos del dominio agregados al texto original antes de pasarlo a `websearch_to_tsquery` |
| `EstadoInferido` | `short?` | Código de `estados_licitacion` si la consulta menciona un estado ("activas", "cerradas", "adjudicadas") y el usuario no pasó `estado` explícito |
| `MontoDesde` / `MontoHasta` | `decimal?` | Rango inferido si la consulta menciona un monto ("mayores a 10 millones") — mapea a `monto_estimado` |
| `FechaDesde` / `FechaHasta` | `date?` | Rango inferido si la consulta menciona un plazo ("cerradas el último mes") |
| `Confianza` | `enum { Alta, Baja }` | Si Gemini no logra interpretar nada útil, `Baja` → se usa `q` tal cual, sin expansión (edge case del spec: "consulta ambigua") |

Si `ConsultaSemanticaService` falla o Vertex no está configurado, este objeto es `null` y el flujo cae al comportamiento actual de `usp_Licitaciones_BuscarNatural` (FR-005, degradación controlada) — igual que hoy.

## Resultado de búsqueda (response)

Reutiliza `LicitacionNaturalSearchResult` (`src/MPM.Modules.Licitaciones/Models/LicitacionResumenDto.cs:60+`), sin cambios de forma: `Id, CodigoExterno, Nombre, Descripcion, Organismo, CodigoEstado, Tipo, Relevancia, ...`. No se agrega score de embeddings porque no hay pipeline vectorial (ver `research.md`).

## Historial de sesión de búsqueda (FR-007)

FR-007 pide "refinar sin perder contexto de la consulta anterior". No requiere persistencia en backend — se resuelve en el frontend manteniendo el estado de la última consulta interpretada (`TerminosExpandidos`, filtros aplicados) en el hook `useBuscarNatural`, para que un refinamiento reenvíe esos filtros ya resueltos en vez de reinterpretar desde cero cada vez que el usuario ajusta un filtro visible (p. ej. cambia el selector de estado sin tocar el texto).
