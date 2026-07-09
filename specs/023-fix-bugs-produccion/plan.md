# Implementation Plan: Corrección urgente de bugs detectados en producción (Mensajería y Alertas)

**Branch**: `023-fix-bugs-produccion` | **Date**: 2026-07-09 | **Spec**: [spec.md](./spec.md)

**Input**: Feature specification from `specs/023-fix-bugs-produccion/spec.md`

## Summary

Dos bugs P1 confirmados en vivo contra producción (`tivit-cu010`) el mismo día del deploy inicial: (1) crear una conversación nueva en Mensajería falla siempre por un mismatch entre el valor `"directa"` que envía el frontend y el valor `'directo'` que acepta el CHECK CONSTRAINT de la tabla `conversaciones`; (2) el auto-vinculado de Telegram en Alertas (deep-link o Chat ID manual) nunca marca `es_account_manager_gobierno = TRUE` en `alertas_destinatarios`, por lo que `usp_AlertasDestinatarios_ListarAccountManagers()` nunca devuelve a esos usuarios y no reciben alertas — sin ningún error visible. El enfoque es un fix quirúrgico en ambos lados (un cambio de string en frontend, un cambio de stored procedure + backfill de una sola fila de migración) sin tocar la lógica de matching de alertas ni el resto del flujo de mensajería.

## Technical Context

**Language/Version**: C# / .NET 8 (backend), TypeScript 5 + React 18 (frontend)

**Primary Dependencies**: Dapper 2.x + Npgsql (BUG-015), React Query + Ant Design `Select`/`Modal` (BUG-014)

**Storage**: PostgreSQL 15+ — tabla `conversaciones` (CHECK CONSTRAINT `conversaciones_tipo_check`) y tabla `alertas_destinatarios` (columna `es_account_manager_gobierno`)

**Testing**: xUnit + FluentAssertions para el stored procedure/handler de Alertas; Playwright o prueba manual dirigida para el flujo de creación de conversación (BUG-014 es puramente un mismatch de valor, bajo riesgo, cubierto también por test de integración existente si aplica)

**Target Platform**: Cloud Run (producción, `tivit-cu010`) + Docker Compose (local)

**Project Type**: Web application (frontend `src/mpm-web` + backend modular monolith `src/MPM.Modules.*`)

**Performance Goals**: N/A — corrección de comportamiento, no de rendimiento

**Constraints**: El fix de BUG-015 debe ser retrocompatible con vías administrativas no-autoservicio que también escriben `es_account_manager_gobierno` (ver FR-007) — no reemplazar esa columna ni su semántica, solo corregir el camino de auto-servicio para que la setee.

**Scale/Scope**: Cambio acotado a 2 archivos de código (1 frontend, 1 backend/stored procedure) + 1 migración de backfill. No requiere cambios de infraestructura ni de esquema nuevo.

## Constitution Check

*GATE: Must pass before Phase 0 research. Re-check after Phase 1 design.*

- **I. Modular Monolith**: Cumple — el fix de BUG-015 se queda dentro de `MPM.Modules.Alertas` (Data/Services), el fix de BUG-014 se queda dentro de `src/mpm-web` (hook `useMensajeria`/tipo en `types/mensajeria.ts`). Ningún módulo nuevo, ninguna referencia cruzada nueva.
- **II. Stored Procedures First**: Cumple — el fix de BUG-015 modifica el stored procedure existente `usp_AlertasDestinatarios_GuardarChatId` (`CREATE OR REPLACE FUNCTION`), no se agrega ORM ni SQL inline nuevo fuera de stored procedures.
- **III. Migraciones como Scripts Embebidos**: Cumple — el `CREATE OR REPLACE FUNCTION` y el `UPDATE` de backfill van en un único script `VXXX__Fix_Alertas_Destinatarios_Telegram.sql`, numerado correlativo al siguiente `VXXX` libre en el repo.
- **IV. Multi-Tenancy por Middleware**: No aplica — ninguno de los dos bugs toca resolución de tenant.
- **V. Abstracción de Storage**: No aplica.
- **VI. Real-Time via SignalR + Redis Backplane**: No aplica — BUG-014 es un problema de creación de conversación vía REST, no del hub en sí (el hub y el envío de mensajes en conversaciones existentes ya están validados y funcionan).
- **VII. Testing por Capas**: Cumple — se agrega un unit test en `tests/MPM.Modules.Alertas.Tests` que verifica que `GuardarChatIdAsync` deja al usuario habilitado (visible a `ListarAccountManagersAsync`), y se agrega/ajusta un test de integración o Playwright para la creación de conversación directa.

**Resultado**: Sin violaciones. No se requiere la sección de Complexity Tracking.

## Project Structure

### Documentation (this feature)

```text
specs/023-fix-bugs-produccion/
├── plan.md              # This file
├── research.md          # Phase 0 output
├── data-model.md         # Phase 1 output
├── quickstart.md         # Phase 1 output
└── tasks.md              # Phase 2 output (/speckit-tasks)
```

### Source Code (repository root)

```text
src/MPM.Modules.Alertas/
├── Data/AlertasHandler.cs          # GuardarChatIdAsync — sin cambios de firma
├── Data/AlertasStoredProcedures.cs # referencia al SP, sin cambios
└── Services/AlertasService.cs      # GuardarMiTelegramAsync / VincularTelegramPorTokenAsync — sin cambios (el fix vive en el SP)

src/MPM.Api/Database/Scripts/
└── VXXX__Fix_Alertas_Destinatarios_Telegram.sql   # CREATE OR REPLACE FUNCTION usp_AlertasDestinatarios_GuardarChatId + UPDATE backfill

src/mpm-web/src/
├── types/mensajeria.ts             # TIPO_CONVERSACION.DIRECTO ya existe como 'directo' — usar esta constante en el modal
└── pages/MensajesPage.tsx (o componente del modal "Nueva conversación")  # reemplazar literal "directa" por TIPO_CONVERSACION.DIRECTO

tests/MPM.Modules.Alertas.Tests/
└── Services/AlertasMatchingServiceTests.cs  # o nuevo archivo — test que vincular Telegram habilita ListarAccountManagersAsync
```

**Structure Decision**: Se reutiliza la estructura de módulos ya existente (`MPM.Modules.Alertas` para el backend, `src/mpm-web` para el frontend) — no se crea ningún proyecto ni módulo nuevo. El fix de BUG-014 es un cambio de una constante/literal en el frontend; el fix de BUG-015 es un `CREATE OR REPLACE FUNCTION` + `UPDATE` de backfill en una migración nueva, siguiendo el mecanismo estándar del proyecto (Principio III).

## Complexity Tracking

*Sin violaciones — sección no aplica.*
