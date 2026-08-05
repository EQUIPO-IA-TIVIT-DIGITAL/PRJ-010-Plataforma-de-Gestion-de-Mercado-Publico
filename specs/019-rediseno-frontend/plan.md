# Implementation Plan: Rediseño Frontend de MPM — Alcance por pantalla

**Branch**: `019-rediseno-frontend` | **Date**: 2026-08-05 | **Spec**: [spec.md](./spec.md)

**Input**: Feature specification from `specs/019-rediseno-frontend/spec.md`

---

## Summary

Evoluciona el sistema de theming de Ant Design 5 ya existente en `src/mpm-web/src/main.tsx` y lo aplica de forma consistente en 7 pantallas priorizadas (Licitaciones, Análisis, Catálogos, Mensajería, Ejecutivo, Alertas, Competidores), reemplazando implementaciones divergentes de badges de estado, headers de página y colores hardcodeados por componentes compartidos nuevos (`StatusBadge`, `PageHeader`). Análisis recibe un rediseño de fondo (P1, junto con la corrección de densidad/alineación de Licitaciones); Catálogos y Mensajería reciben una actualización de UX/integración (P2); Ejecutivo, Alertas y Competidores reciben pulido visual, y Ejecutivo además incorpora datos comparativos nuevos (P3). Notificaciones queda fuera de alcance. Enfoque "Redesign - Preserve": auditar, extraer tokens, evolucionar el sistema existente — sin proyecto frontend paralelo, sin librerías de motion/animación nuevas, sin tocar lógica de negocio ni endpoints salvo lo estrictamente necesario para las comparativas nuevas de Ejecutivo.

---

## Technical Context

**Language/Version**: React 18 + TypeScript 5, Ant Design 5 (`ConfigProvider`/tokens)

**Primary Dependencies**: Ninguna nueva prevista para las historias P1/P2. Para Ejecutivo (P3, FR-008) es posible que se necesite una librería de gráficos si la comparativa elegida no se resuelve con los componentes de Ant Design (`Statistic`, `Progress`, `Table`) — a confirmar en Fase 0 (research.md) según qué comparativa se elija.

**Storage**: N/A para la mayoría de historias — sin cambios de esquema. Si Ejecutivo (FR-008) requiere una comparativa que no se calcula hoy en backend, podría implicar un endpoint nuevo de solo lectura sobre datos ya existentes (`licitaciones`, `licitaciones_ofertas`, `analisis_workspaces`) — sin tabla nueva prevista.

**Testing**: Playwright E2E existente en `src/mpm-web/e2e/` (Principio VII de la constitución) — se extiende con specs de regresión visual/funcional por pantalla rediseñada, no se reemplaza el framework de test.

**Target Platform**: Web (navegador), responsive — Licitaciones (US1) requiere validación explícita en viewport `< 768px`.

**Project Type**: Web application — frontend-only dentro del monorepo existente (`src/mpm-web`), sin cambios de backend salvo la posible excepción de Ejecutivo.

**Performance Goals**: Sin regresión de performance de render en tablas grandes (Licitaciones puede listar miles de registros) — mismos componentes `Table` de Ant Design ya en uso, solo se ajustan tokens de estilo y composición.

**Constraints**: Ninguna librería de motion/animación nueva (GSAP, Framer Motion/Motion) — ver Assumptions de spec.md. Cambios exclusivamente dentro de `src/mpm-web`, reusando su `package.json` existente salvo justificación puntual explícita.

**Scale/Scope**: 7 pantallas + 2 componentes compartidos nuevos (`StatusBadge`, `PageHeader`) + reconstrucción de `MensajeriaPage.tsx` y sus subcomponentes.

---

## Constitution Check

*GATE: Must pass before Phase 0 research. Re-check after Phase 1 design.*

| Principio | Estado | Justificación |
|---|---|---|
| **I. Modular Monolith** | ✅ N/A | No afecta módulos backend — cambio exclusivamente en `src/mpm-web` |
| **II. Stored Procedures First** | ⚠️ Condicional | Solo aplica si Ejecutivo (FR-008) termina requiriendo un endpoint nuevo — en ese caso, el stored procedure nuevo sigue la convención `usp_<Entidad>_<Verbo>` sin excepción. Se resuelve en Fase 0. |
| **III. Migraciones como Scripts Embebidos** | ⚠️ Condicional | Solo si Ejecutivo requiere una consulta que no puede resolverse con los `usp_*` existentes — se documentaría como migración `VXXX__*.sql` nueva, no como excepción. |
| **IV. Multi-Tenancy por Middleware** | ✅ Sin violación | Sin cambios de auth/tenant; si hay endpoint nuevo (Ejecutivo), reutiliza `TenantContext` ya inyectado como el resto de la API |
| **V. Abstracción de Storage** | ✅ N/A | Sin cambios de almacenamiento de archivos |
| **VI. Real-Time via SignalR + Redis** | ✅ Sin violación | Mensajería (US4) se reconstruye visualmente sobre el mismo hub/hooks (`useChatLogic`, `usePresencia`) ya existentes — no se toca el mecanismo de tiempo real |
| **VII. Testing por Capas** | ✅ Cumple | Se extiende la suite Playwright E2E existente con validación por pantalla rediseñada; no se requieren unit tests de backend nuevos salvo que Ejecutivo agregue un endpoint (en ese caso, mismo estándar xUnit+Moq+FluentAssertions que el resto del backend) |

Sin violaciones no justificadas. Los dos ítems condicionales (II, III) dependen de una decisión que se resuelve en Fase 0, no de una excepción a la constitución.

---

## Project Structure

### Documentation (this feature)

```text
specs/019-rediseno-frontend/
├── plan.md              # Este archivo
├── research.md          # Fase 0 — inventario de inconsistencias por pantalla, decisión de tokens/componentes compartidos, decisión sobre Ejecutivo FR-008
├── data-model.md         # Fase 1 — entidades de UI (StatusBadge, PageHeader) y, si aplica, entidad de datos nueva para Ejecutivo
├── quickstart.md        # Fase 1 — checklist de validación visual/funcional por pantalla
├── contracts/           # Fase 1 — solo si Ejecutivo requiere endpoint nuevo; si no, carpeta vacía u omitida
└── tasks.md             # Fase 2 (/speckit-tasks) — NO se genera en este comando
```

### Source Code (repository root)

```text
src/mpm-web/src/
├── main.tsx                        ← theme de Ant Design ya existente (ConfigProvider); se completa con tokens faltantes, no se reemplaza
├── components/
│   ├── StatusBadge.tsx             ← NUEVO — indicador de estado unificado, reemplaza las 5 implementaciones divergentes encontradas en la auditoría
│   ├── PageHeader.tsx              ← NUEVO — header de página unificado, reemplaza las 3 estructuras divergentes encontradas
│   ├── AppLayout.tsx                ← existente, sin cambios de estructura previstos
│   ├── LicitacionesTable.tsx        ← ajustes de composición (US1: grilla de estadísticas sin huecos, densidad)
│   ├── LicitacionFilterBar.tsx      ← sin cambios funcionales, solo adopción de tokens/PageHeader donde aplique
│   ├── AnalisisChat.tsx             ← rediseño (parte de US2)
│   ├── ComparativaDocumentos.tsx    ← rediseño (parte de US2)
│   ├── ChatPanel.tsx / ChatHeader.tsx / MensajeList.tsx / MensajeInput.tsx / TypingIndicator.tsx / ConversacionList.tsx
│   │                                 ← reconstrucción sobre componentes de Ant Design (US4), preservando hooks existentes (useChatLogic, usePresencia)
│   └── ... (resto de componentes compartidos, adoptan StatusBadge/PageHeader donde corresponda)
└── pages/
    ├── LicitacionesPage.tsx         ← US1 (P1)
    ├── AnalisisListPage.tsx         ← US2 (P1)
    ├── AnalisisWorkspacePage.tsx    ← US2 (P1)
    ├── AnalisisDashboardPage.tsx    ← US2 (P1)
    ├── AnalisisChatPage.tsx         ← US2 (P1)
    ├── CatalogoPage.tsx             ← US3 (P2)
    ├── MensajeriaPage.tsx           ← US4 (P2) — reconstrucción completa, hoy es 100% div+estilos inline
    ├── EjecutivoDashboardPage.tsx   ← US5 (P3)
    ├── AlertasPage.tsx              ← US5 (P3)
    ├── CompetidoresPage.tsx         ← US5 (P3)
    └── NotificacionesPage.tsx       ← fuera de alcance (FR-012), sin cambios salvo herencia automática de layout compartido
```

**Structure Decision**: Todo el trabajo vive dentro de `src/mpm-web` ya existente — sin proyecto paralelo (lección explícita del intento descartado de `mpm-web-v2` en esta misma sesión, ver Contexto de spec.md). Se introducen 2 componentes compartidos nuevos (`StatusBadge`, `PageHeader`) en `src/components/` junto a los existentes. La reconstrucción de Mensajería reutiliza los hooks de datos/tiempo-real ya existentes (`useChatLogic`, `usePresencia`) — solo cambia la capa de presentación.

---

## Orden de ejecución sugerido (por prioridad de spec.md, pantalla por pantalla)

1. **Base compartida** (bloquea al resto): `StatusBadge` y `PageHeader` — un único desarrollo reutilizado por las 7 pantallas.
2. **US1 — Licitaciones** (P1): grilla de estadísticas sin huecos, reducción de densidad, validación responsive.
3. **US2 — Análisis** (P1): lista → workspace → dashboard → chat, en ese orden (cada uno depende visualmente del anterior para mantener coherencia dentro del propio módulo).
4. **US3 — Catálogos** (P2).
5. **US4 — Mensajería** (P2): la reconstrucción más grande fuera de Análisis: reemplaza toda la capa de presentación manteniendo los hooks de datos intactos.
6. **US5 — Ejecutivo, Alertas, Competidores** (P3): pulido visual en las tres; Ejecutivo además incorpora la comparativa nueva de FR-008 (requiere que Fase 0 haya resuelto si necesita endpoint nuevo).

Cada paso puede pausarse y retomarse de forma independiente (FR-010) — ninguna pantalla queda visualmente a medio camino porque cada una adopta `StatusBadge`/`PageHeader` de forma atómica antes de darse por cerrada.

## Artefactos pendientes (Fase 0 y Fase 1 de este comando)

- [ ] `research.md` — decisión final de los tokens/props de `StatusBadge` y `PageHeader`; inventario completo de estados semánticos existentes en el sistema (licitación, análisis, notificación, alerta, conversación) que `StatusBadge` debe cubrir; decisión sobre qué comparativa(s) agregar a Ejecutivo (FR-008) y si requiere endpoint nuevo.
- [ ] `data-model.md` — props/variants de `StatusBadge` y `PageHeader`; si Ejecutivo requiere datos nuevos, el modelo de esa respuesta.
- [ ] `contracts/` — solo si Ejecutivo requiere endpoint nuevo (contrato HTTP del mismo, siguiendo el formato ya usado en otras specs, ej. `specs/031-feedback-chilecompra/contracts/`).
- [ ] `quickstart.md` — checklist de validación por pantalla (visual + funcional, incluyendo el caso responsive de US1 y el caso de no-pérdida-de-funcionalidad de Mensajería).
- [ ] `tasks.md` — generado con `/speckit-tasks`, estructurado en el mismo orden de ejecución sugerido arriba, para poder pausarse entre pantallas sin dejar trabajo a medias.

## Complexity Tracking

*Sin violaciones de constitución que requieran justificación — tabla omitida.*
