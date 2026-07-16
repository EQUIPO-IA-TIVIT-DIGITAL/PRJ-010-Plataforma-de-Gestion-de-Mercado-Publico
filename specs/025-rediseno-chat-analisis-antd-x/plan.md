# Implementation Plan: Rediseño del chat de IA en Análisis con Ant Design X

**Branch**: `025-rediseno-chat-analisis-antd-x` | **Date**: 2026-07-10 | **Spec**: [spec.md](./spec.md)

**Input**: Feature specification from `specs/025-rediseno-chat-analisis-antd-x/spec.md`

## Summary

Rediseño puramente de presentación del componente `AnalisisChat.tsx` (chat de IA sobre un análisis, usado en `/analisis/{id}/chat` y embebido en el dashboard): reemplazar las burbujas y el compositor armados a mano (divs con estilos inline, indicador de "escribiendo" en CSS) por los componentes reales de `@ant-design/x` (`Bubble.List`, `Bubble`, `Sender`), manteniendo el mismo contrato de datos (`useEnviarChat`/`useChatHistorial`), el mismo renderizado de markdown, y la misma paleta de colores TIVIT ya usada en el resto de la app. No hay cambios de backend, de modelo de datos, ni de arquitectura (el chat sigue siendo no-streaming).

## Technical Context

**Language/Version**: TypeScript 5 + React 18.3 (frontend únicamente — sin cambios de backend)

**Primary Dependencies**: `@ant-design/x@1.6.1` (nuevo — ver research.md R1/R2 sobre por qué esta versión y no la última), `antd` (bump de `^5.18.0` a `^5.20.3`, sigue siendo v5), `react-markdown` (se mantiene, reutilizado dentro de `contentRender`)

**Storage**: N/A — sin cambios de base de datos ni de API

**Testing**: Playwright E2E existente (`src/mpm-web/e2e/`) — se agrega/ajusta un caso que cubra enviar un mensaje y ver la respuesta renderizada con los componentes nuevos; sin tests de backend nuevos (no hay cambios de backend)

**Target Platform**: Web (Cloud Run `mpm-web` en producción, Docker Compose en local) — mismo target que hoy

**Project Type**: Frontend-only (React SPA existente, `src/mpm-web`)

**Performance Goals**: Sin cambios — la latencia de respuesta la determina el backend (Gemini vía Vertex AI, no-streaming), no esta spec

**Constraints**: No romper la funcionalidad existente del chat (historial, mensaje optimista, markdown, guard de mensaje vacío) — ver FR-005 en spec.md. No forzar un upgrade de Ant Design 5→6.

**Scale/Scope**: Un componente compartido (`AnalisisChat.tsx`) usado en 2 superficies (`AnalisisChatPage.tsx`, dashboard embebido) — sin cambios de alcance de datos.

## Constitution Check

*GATE: Must pass before Phase 0 research. Re-check after Phase 1 design.*

- **I. Modular Monolith**: No aplica — no hay cambios de backend ni de módulos .NET.
- **II. Stored Procedures First**: No aplica — sin cambios de acceso a datos.
- **III. Migraciones como Scripts Embebidos**: No aplica — sin cambios de esquema.
- **IV. Multi-Tenancy por Middleware**: No aplica — sin cambios de backend; nota relacionada (fuera de alcance) en spec.md sobre que el chat hoy es por `workspace_id` sin `user_id`.
- **V. Abstracción de Storage**: No aplica.
- **VI. Real-Time via SignalR + Redis Backplane**: No aplica — el chat de análisis nunca usó SignalR (a diferencia de Mensajería), sigue siendo request/response simple.
- **VII. Testing por Capas**: Se ajusta el test E2E de Playwright existente que cubre el chat (si existe) para que siga pasando contra los componentes nuevos; no aplica testing de backend (sin cambios ahí).

**Resultado**: Sin violaciones. Feature acotada a una sola capa (presentación de un componente React ya existente).

## Project Structure

### Documentation (this feature)

```text
specs/025-rediseno-chat-analisis-antd-x/
├── plan.md          (este archivo)
├── research.md
├── spec.md
└── tasks.md
```

### Source Code (repository root)

```text
src/mpm-web/
├── package.json                          # + @ant-design/x@1.6.1, bump antd a ^5.20.3
└── src/
    ├── components/
    │   └── AnalisisChat.tsx              # REESCRITO: Bubble.List + Bubble + Sender en vez de divs manuales
    └── pages/
        └── AnalisisChatPage.tsx          # sin cambios de lógica -- sigue embebiendo <AnalisisChat />
```

No hay cambios en `src/MPM.Api`, `src/MPM.Modules.*`, ni en `tests/` de .NET — es exclusivamente `src/mpm-web`.

## Complexity Tracking

*No hay violaciones de la Constitution que justificar — tabla omitida.*
