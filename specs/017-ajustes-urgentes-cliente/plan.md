# Implementation Plan: Ajustes Urgentes del Cliente — UI/UX, Sesión y Coherencia del Análisis

**Branch**: `017-ajustes-urgentes-cliente` | **Date**: 2026-07-01 | **Spec**: [spec.md](spec.md)

**Input**: Feature specification from `/specs/017-ajustes-urgentes-cliente/spec.md`

## Summary

Lote de ajustes urgentes en dos frentes: (1) **frontend** — manejo centralizado de sesión expirada (401 → login), simplificación de login, sidebar con rol y sin correo, borrar notificaciones, rediseño compacto de Licitaciones (un solo buscador, sin búsqueda inteligente ni botón sincronizar, reiniciar filtros), explicaciones en catálogos, chat contextual en vista propia, exportación PDF estructurada (texto real); (2) **backend** — endpoints DELETE de notificaciones con sus stored procedures, sincronización automática semanal cubriendo 2025-2026, y mejora del análisis Gemini con validación cruzada de documentos enviados vs. veredicto del acta (`validacion_documental` + comparativa de documentos en el resumen). Además, una investigación documental (sin código) sobre factibilidad de explicar por qué se ganan licitaciones.

## Technical Context

**Language/Version**: C# / .NET 8 (backend), TypeScript 5 + React 18 (frontend)

**Primary Dependencies**: Dapper + Npgsql (SPs PostgreSQL), Ant Design 5, TanStack Query v5, React Router 6, `react-markdown`, `jspdf` (se agrega `jspdf-autotable`), Google Gemini API (`GeminiService`)

**Storage**: PostgreSQL 16 vía stored procedures `usp_*`; migraciones embebidas `VXXX__*.sql` (siguiente número: **V075**)

**Testing**: xUnit + Moq + FluentAssertions (unit por módulo), `MPM.Tests` (integración HTTP), Playwright E2E (`src/mpm-web/e2e/`)

**Target Platform**: Docker Compose (API :5001, Web :8181, DB :5433); GCP en producción

**Project Type**: Web application — monolito modular .NET + SPA React

**Performance Goals**: Sin cambios de carga; el PDF estructurado debe generarse en < 5 s para análisis extensos; la redirección por 401 debe ser inmediata (< 1 s percibido)

**Constraints**: Sin ORM (SPs only); módulos no se referencian entre sí; migraciones solo vía `Database/Scripts`; multi-tenancy vía `TenantContext`; los cambios deben estar listos para la revisión del cliente de mañana (priorizar US1–US3)

**Scale/Scope**: ~8 pantallas afectadas, 2 módulos backend (Notificaciones, Licitaciones/sync, Analisis), 1 migración nueva, sin entidades de dominio nuevas persistidas (la validación documental viaja dentro del JSON de análisis existente)

## Constitution Check

*GATE: Must pass before Phase 0 research. Re-check after Phase 1 design.*

| Principio | Estado | Justificación |
|-----------|--------|---------------|
| I. Modular Monolith | ✅ PASS | Cambios dentro de módulos existentes (Notificaciones, Licitaciones, Analisis); sin módulos nuevos ni referencias cross-module |
| II. Stored Procedures First | ✅ PASS | Borrado de notificaciones vía nuevos SPs `usp_Notificaciones_Eliminar` / `usp_Notificaciones_EliminarTodas` llamados con Dapper |
| III. Migraciones embebidas | ✅ PASS | Única migración nueva: `V075__Notificaciones_Eliminar_SPs.sql` |
| IV. Multi-Tenancy | ✅ PASS | Los SPs de borrado reciben `p_tenant_id`/`p_user_id` del `TenantContext`, igual que los existentes |
| V. Storage abstraction | ✅ PASS | No se toca storage |
| VI. SignalR + Redis | ✅ PASS | Solo se agrega desconexión ordenada del hub al expirar sesión (frontend) |
| VII. Testing por capas | ✅ PASS | Unit tests para SPs handlers y servicios modificados; integración para DELETE notificaciones; E2E para flujo 401→login y pantalla licitaciones |

**Post-diseño (re-check)**: ✅ PASS — el diseño no introdujo violaciones; la validación documental se almacena dentro del JSON de resultado de análisis existente (sin tablas nuevas).

## Project Structure

### Documentation (this feature)

```text
specs/017-ajustes-urgentes-cliente/
├── plan.md              # Este archivo
├── research.md          # Fase 0 — decisiones técnicas
├── data-model.md        # Fase 1 — estructuras de datos
├── quickstart.md        # Fase 1 — guía de validación
├── contracts/
│   └── api.md           # Contratos HTTP nuevos/modificados
└── tasks.md             # Fase 2 (/speckit-tasks — no creado aquí)
```

### Source Code (repository root)

```text
src/MPM.Api/Database/Scripts/
└── V075__Notificaciones_Eliminar_SPs.sql        # NUEVO: SPs de borrado

src/MPM.Modules.Notificaciones/
├── Controllers/NotificacionesController.cs       # + DELETE {id}, DELETE (todas)
├── Services/NotificacionesService.cs             # + EliminarAsync / EliminarTodasAsync
└── Data/NotificacionesDbHandler.cs (+ SP names)  # + llamadas Dapper a nuevos SPs

src/MPM.Modules.Licitaciones/
└── Services/SyncEngineService.cs                 # cadencia semanal + ventana 2025-2026 + log de fallos

src/MPM.Modules.Analisis/
├── Services/GeminiService.cs                     # prompt: sección validacion_documental + profundidad
└── Services/AnalisisService.cs                   # post-proceso: cruce requeridos/enviados/acta

src/mpm-web/src/
├── lib/apiClient.ts                              # NUEVO: fetch central con manejo 401
├── hooks/*.ts                                    # migrar fetch crudo → apiClient (patrón repetido)
├── hooks/useAuth.ts                              # sessionExpired(): limpieza + redirect único
├── pages/LoginPage.tsx                           # quitar link "¿Olvidaste tu contraseña?"
├── components/AppLayout.tsx                      # sidebar: avatar + rol "admin TIVIT", sin email
├── pages/NotificacionesPage.tsx + useNotificaciones.ts  # acción eliminar (una/todas)
├── pages/LicitacionesPage.tsx                    # rediseño compacto, sin smart search ni sync
├── components/LicitacionFilterBar.tsx            # buscador único + "Reiniciar filtros"
├── components/LicitacionSearchBar.tsx            # ELIMINAR (duplicado)
├── components/SyncButton.tsx                     # ELIMINAR (sync pasa a ser solo automático)
├── pages/CatalogoPage.tsx                        # click → Drawer con explicación del concepto
├── pages/AnalisisDashboardPage.tsx               # extraer chat; agregar ComparativaDocumentos; PDF estructurado
├── pages/AnalisisChatPage.tsx                    # NUEVO: vista propia del chat contextual (/analisis/:id/chat)
├── components/AnalisisChat.tsx                   # NUEVO: chat extraído y reutilizable
├── lib/analisisPdf.ts                            # NUEVO: generación PDF estructurada (jspdf + autotable)
└── pages/EjecutivoDashboardPage.tsx              # mejoras visuales (jerarquía, tarjetas)

docs/
└── investigacion-victorias-licitaciones.md      # NUEVO: entregable US6 (solo documento)
```

**Structure Decision**: Web application existente — se reutilizan los módulos backend actuales y la SPA React. El único artefacto transversal nuevo en frontend es `lib/apiClient.ts` (wrapper de fetch), del que migran todos los hooks; en backend no hay módulos nuevos.

## Complexity Tracking

Sin violaciones de la constitución — tabla no aplica.
