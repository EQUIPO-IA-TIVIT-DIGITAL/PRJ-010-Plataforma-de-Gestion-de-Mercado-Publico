# Implementation Plan: Frontend de Licitaciones Alineado al Catálogo Real de Tipos/Estados

**Branch**: `027-catalogo-frontend-licitaciones-generales` | **Date**: 2026-07-16 | **Spec**: [spec.md](./spec.md)

**Input**: Feature specification from `specs/027-catalogo-frontend-licitaciones-generales/spec.md`

## Summary

El selector de Tipo del buscador de licitaciones hoy no filtra sobre datos reales (el catálogo solo tiene 4 categorías genéricas contra 16 códigos reales que ya existen en la tabla `licitaciones`), y el selector de Estado muestra duplicados heredados de un catálogo anterior. Además, la tabla de licitaciones generales muestra columnas de Organismo/Monto/Items que están vacías para prácticamente el 100% de esas ~178 mil licitaciones (limitación conocida de la API masiva, no un error). Este plan: (1) repuebla el catálogo de tipos con los códigos reales del glosario de la spec 026, cambiando su clave de número a texto; (2) filtra el catálogo de estados a los 5 códigos reales vigentes sin tocar datos existentes; (3) quita las tres columnas ruidosas de la tabla del listado, apoyándose en el enriquecimiento bajo demanda ya existente para seguir mostrándolas en el detalle.

## Technical Context

**Lenguaje/Versión**: .NET 8 + React 18 + TypeScript
**Storage**: PostgreSQL — cambio de tipo de columna en `tipos_licitacion.codigo` (SMALLINT → VARCHAR); `estados_licitacion` sin cambios de esquema ni de datos
**Testing**: xUnit + Moq + FluentAssertions (backend), sin tests E2E nuevos previstos — cambio de UI validado manualmente vía quickstart.md
**Target**: Módulos `MPM.Modules.Catalogo` (backend) y `src/mpm-web` (frontend) — sin módulo nuevo
**Constraints**: `tipos_licitacion.codigo` no tiene FK entrante desde `licitaciones.tipo` (confirmado en research.md) — el cambio de tipo de clave no requiere migrar datos de la tabla `licitaciones`. `estados_licitacion.codigo` sí tiene FK activa: no se puede borrar ni renumerar sin antes remapear las 144 licitaciones que usan el código heredado 1 (fuera de alcance, ver spec 028 parqueada)
**Scale/Scope**: ~178,391 licitaciones generales afectadas por la falta de catálogo de tipo real; cambio de UI aplica a todas ellas de forma transversal (no requiere procesar fila por fila)

## Constitution Check

*GATE: Must pass before Phase 0 research. Re-check after Phase 1 design.*

| Principio | Estado | Justificación |
|---|---|---|
| **I. Modular Monolith** | ✅ Sin violación | Cambios contenidos en `MPM.Modules.Catalogo` (backend) y componentes de licitaciones en frontend — no cruza límites de módulo |
| **II. Stored Procedures First** | ✅ Aplicar | Todo acceso a `tipos_licitacion`/`estados_licitacion` sigue pasando por `usp_Catalogos_TiposLicitacion()`/`usp_Catalogos_EstadosLicitacion()` vía Dapper |
| **III. Migraciones SQL** | ✅ Aplicar | Migración nueva para repoblar `tipos_licitacion` y ajustar `usp_Catalogos_EstadosLicitacion()` — siguiente libre a confirmar contra el estado real de `Scripts/` al implementar (V108 al momento de este plan) |
| **IV. Multi-Tenancy** | ✅ Sin cambios | Catálogos no son sensibles a tenant |
| **V. Abstracción de Storage** | N/A | No involucra archivos |

Sin violaciones — no requiere Complexity Tracking.

## Project Structure

### Documentation (this feature)

```text
specs/027-catalogo-frontend-licitaciones-generales/
├── plan.md              # Este archivo
├── research.md          # Decisiones: clave de tipos a texto, filtro de estados sin borrar datos, columnas fuera de la tabla
├── data-model.md         # Cambios de esquema y DTOs
├── quickstart.md        # 4 escenarios de validación
└── contracts/
    └── catalogos-api.md # Contrato de los 2 endpoints de catálogo afectados
```

### Source Code (repository root)

```text
src/MPM.Api/Database/Scripts/
└── V108__Reconciliar_Catalogo_Tipos_Estados.sql   # Repuebla tipos_licitacion (codigo VARCHAR),
                                                     # ajusta usp_Catalogos_EstadosLicitacion()

src/MPM.Modules.Catalogo/
├── Models/CatalogoDtos.cs          # TipoLicitacionItemDto.Codigo: int → string
└── Data/CatalogoHandler.cs         # Sin cambio de lógica, solo el tipo genérico de QueryAsync<T>

src/mpm-web/src/
├── types/catalogo.ts               # TipoLicitacionItem.codigo: number → string
├── types/licitacion.ts             # LicitacionFilter.tipo: TipoLicitacion|null → string|null
├── components/LicitacionFilterBar.tsx  # value del Select de Tipo: t.codigo (string) en vez de t.slug
└── components/LicitacionesTable.tsx    # quitar columnas Organismo, Monto, Items
```

**Structure Decision**: sin módulo nuevo. Cambios acotados a `MPM.Modules.Catalogo` (única fuente de los catálogos) y a los componentes de licitaciones ya existentes en frontend — no se toca `LicitacionDetailDrawer.tsx` (FR-005: el detalle sigue mostrando esos campos vía el enriquecimiento bajo demanda ya existente en `LicitacionService.ObtenerPorCodigoAsync`).

## Complexity Tracking

*Sin violaciones de constitución — tabla no aplica.*
