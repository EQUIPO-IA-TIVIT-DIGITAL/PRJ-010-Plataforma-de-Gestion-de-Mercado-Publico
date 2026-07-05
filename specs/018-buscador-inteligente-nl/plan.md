# Implementation Plan: Buscador Inteligente en Lenguaje Natural sobre Licitaciones

**Branch**: `018-buscador-inteligente-nl` | **Status**: PENDIENTE
**Spec**: [spec.md](./spec.md) | **Semana**: 3 (Julio 2026)

> Ejecutar `/speckit-plan` para completar: research.md, data-model.md, contracts/, quickstart.md, tasks.md

---

## Summary

Reemplaza la búsqueda literal actual (`buscar-natural` / `usp_Licitaciones_BuscarNatural`, full-text `plainto_tsquery`) por una capa de interpretación semántica real: extrae conceptos, sinónimos y filtros implícitos (monto, ubicación, estado) de una consulta en lenguaje natural, y conecta el resultado a una barra de búsqueda visible en el módulo de Licitaciones (hoy el hook `useBuscarNatural` existe pero no está usado por ninguna página).

---

## Technical Context

**Lenguaje**: .NET 8 + React 18 + TypeScript
**Dependencias nuevas**: Proveedor de embeddings/IA para interpretación de consultas — a evaluar en `research.md` (reutilizar Gemini ya integrado en `MPM.Modules.Analisis` vs. `pgvector` sobre PostgreSQL vs. servicio externo de embeddings)
**Storage**: PostgreSQL — posible extensión de `search_vector` existente o nueva columna/tabla de embeddings si se opta por búsqueda vectorial
**Módulo afectado**: `MPM.Modules.Licitaciones` (no requiere módulo nuevo — extiende `LicitacionController`/`LicitacionService`/`LicitacionHandler` existentes)
**Fallback obligatorio**: Degradar a `usp_Licitaciones_BuscarNatural` (full-text) si el componente semántico falla (FR-005)
**Estimación**: 1-1.5 semanas | **Complejidad**: Media-Alta

---

## Module Structure

**Módulo existente, sin módulo nuevo**: `MPM.Modules.Licitaciones`

```text
src/MPM.Modules.Licitaciones/
├── Controllers/
│   └── LicitacionController.cs          ← extender endpoint buscar-natural existente
├── Services/
│   ├── LicitacionService.cs             ← orquesta interpretación + fallback
│   └── ConsultaSemanticaService.cs      ← NUEVO: interpreta lenguaje natural → filtros + ranking
├── Data/
│   └── LicitacionHandler.cs             ← extender BuscarNaturalAsync con nuevos parámetros de filtro

src/MPM.Api/Database/Scripts/
└── V078__Extend_BuscarNatural.sql       ← ajustes de índice/columnas según decisión en research.md

src/mpm-web/src/
├── pages/LicitacionesPage.tsx           ← conectar barra de búsqueda semántica (reemplaza filtro simple actual)
└── hooks/useLicitaciones.ts             ← reactivar y extender useBuscarNatural existente
```

---

## Constitution Check

| Principio | Estado | Justificación |
|---|---|---|
| **I. Modular Monolith** | ✅ Sin violación | Se extiende `MPM.Modules.Licitaciones`, no cruza límites de módulo |
| **II. Stored Procedures First** | ✅ Aplicar | Toda consulta pasa por `usp_Licitaciones_BuscarNatural` extendido o su sucesor, vía Dapper |
| **III. Migraciones SQL** | ✅ Aplicar | Próxima migración libre tras V077 (confirmar número exacto al momento de implementar) |
| **IV. Multi-Tenancy** | ✅ Aplicar | Sin cambios — la búsqueda no es sensible a tenant más allá del contexto ya inyectado en el módulo |
| **V. Abstracción de Storage** | N/A | No involucra archivos |

---

## Artefactos pendientes

- [ ] `research.md` — decisión entre (a) prompt a Gemini para extraer filtros + `tsquery` enriquecido, (b) embeddings + `pgvector`, (c) híbrido; y benchmark de latencia contra SC-001 (<3s)
- [ ] `data-model.md` — estructura de "Consulta de búsqueda" y "Resultado de búsqueda" (ver Key Entities en spec.md)
- [ ] `contracts/buscar-natural-api.md` — contrato extendido del endpoint (parámetros de estado, monto, ubicación; forma del score de relevancia)
- [ ] `quickstart.md` — escenarios de validación de SC-001 a SC-004
- [ ] `tasks.md` — generado con `/speckit-tasks`
