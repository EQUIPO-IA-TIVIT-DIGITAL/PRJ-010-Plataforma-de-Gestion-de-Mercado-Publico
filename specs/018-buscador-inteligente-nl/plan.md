# Implementation Plan: Buscador Inteligente en Lenguaje Natural sobre Licitaciones

**Branch**: `018-buscador-inteligente-nl` | **Status**: PLANIFICADO (research/data-model/contracts/quickstart completos, falta `tasks.md`)
**Spec**: [spec.md](./spec.md) | **Planificado**: 2026-07-16

---

## Summary

Reemplaza la búsqueda literal actual (`buscar-natural` / `usp_Licitaciones_BuscarNatural`, full-text `plainto_tsquery`) por una capa de interpretación en lenguaje natural vía Gemini (mismo patrón que `SinonimosIaService` en Alertas), que expande sinónimos e infiere filtros implícitos (estado, monto, fecha) antes de delegar al motor de filtrado/paginación ya existente (`usp_Licitaciones_Listar`). Conecta el resultado a una barra de búsqueda real en `/licitaciones` — hoy el hook `useBuscarNatural` existe pero no está usado por ninguna página.

**Decisión clave de esta ronda de planning** (ver `research.md`): NO se construye infraestructura de embeddings/pgvector — no existe hoy en el proyecto y no está justificada por el volumen de datos ni por SC-001. Se reutiliza IA (Gemini) + full-text search léxico enriquecido, ambos ya construidos y probados en producción en otros módulos.

---

## Technical Context

**Lenguaje**: .NET 8 + React 18 + TypeScript
**IA**: `gemini-2.5-flash-lite` vía Vertex AI + ADC (USD 0.10/0.40 por millón de tokens entrada/salida — el más barato que cubre esta categoría de extracción; ver `research.md`) — mismo patrón que `SinonimosIaService` (`MPM.Modules.Alertas`), replicado localmente en `MPM.Modules.Licitaciones` (Principio I: cada módulo construye su propia llamada, no se comparte cliente entre módulos). Alternativa de escalón si el recall no alcanza SC-002: `gemini-3.1-flash-lite`
**Storage**: PostgreSQL — sin cambios de esquema. Reutiliza `search_vector` (V066) y los filtros de `usp_Licitaciones_Listar` (V093)
**Módulo afectado**: `MPM.Modules.Licitaciones` (no requiere módulo nuevo)
**Fallback obligatorio**: Si Gemini falla, no está configurado, o la interpretación tiene confianza baja → usar `q` tal cual, comportamiento idéntico al `buscar-natural` actual (FR-005)
**Fuera de alcance confirmado**: filtro por ubicación/región (no existe columna en `licitaciones`, ver `research.md`); búsqueda vectorial/embeddings (plan B si el enfoque léxico+IA no cumple SC-002 en validación real)
**Estimación**: 1-1.5 semanas | **Complejidad**: Media (bajó de Media-Alta tras confirmar que no se necesita pgvector)

---

## Module Structure

**Módulo existente, sin módulo nuevo**: `MPM.Modules.Licitaciones`

```text
src/MPM.Modules.Licitaciones/
├── Controllers/
│   └── LicitacionController.cs          ← sin cambio de firma en buscar-natural (ver contracts/)
├── Services/
│   ├── LicitacionService.cs             ← orquesta: interpretar → enriquecer → delegar a Listar/BuscarNatural
│   └── ConsultaSemanticaService.cs      ← NUEVO: mismo patrón que SinonimosIaService, adaptado a extraer filtros
├── Data/
│   └── LicitacionHandler.cs             ← sin cambios de esquema; reusa usp_Licitaciones_Listar existente

src/mpm-web/src/
├── pages/LicitacionesPage.tsx           ← conectar barra de búsqueda semántica
├── hooks/useLicitaciones.ts             ← reactivar useBuscarNatural, mantener contexto de sesión (FR-007)
└── components/LicitacionFilterBar.tsx   ← evaluar si la barra NL reemplaza o convive con el input actual
```

Sin migración SQL prevista — no hay cambio de esquema (ver `data-model.md`). Si algo emerge durante implementación, la siguiente migración libre es **V107** (confirmar contra el estado real de `src/MPM.Api/Database/Scripts/` al momento de implementar, no asumir).

---

## Constitution Check

| Principio | Estado | Justificación |
|---|---|---|
| **I. Modular Monolith** | ✅ Sin violación | Se extiende `MPM.Modules.Licitaciones`; `ConsultaSemanticaService` replica el patrón de Alertas en vez de referenciarlo cruzando el límite de módulo |
| **II. Stored Procedures First** | ✅ Aplicar | Toda consulta sigue pasando por SPs existentes (`usp_Licitaciones_Listar`, `usp_Licitaciones_BuscarNatural`) vía Dapper — sin SQL ad-hoc en C# |
| **III. Migraciones SQL** | N/A esperado | No se anticipa cambio de esquema; si aparece uno, sería V107 (verificar número real al implementar) |
| **IV. Multi-Tenancy** | ✅ Sin cambios | La búsqueda no es sensible a tenant más allá del contexto ya inyectado |
| **V. Abstracción de Storage** | N/A | No involucra archivos |

Sin violaciones — no requiere justificación adicional en Complexity Tracking.

---

## Artefactos generados en esta ronda (2026-07-16)

- [x] `research.md` — reanálisis de infraestructura existente + decisión (Gemini + tsquery enriquecido, sin pgvector)
- [x] `data-model.md` — DTOs de interpretación (sin cambio de esquema DB)
- [x] `contracts/buscar-natural-api.md` — contrato del endpoint extendido (sin cambio de firma pública)
- [x] `quickstart.md` — 5 escenarios de validación (sinónimos, filtro implícito, filtro explícito con prioridad, degradación, cero-descarga)
- [ ] `tasks.md` — pendiente, generar con `/speckit-tasks`
