---
description: "Task list for Rediseño del chat de IA en Análisis con Ant Design X (025-rediseno-chat-analisis-antd-x)"
---

# Tasks: Rediseño del chat de IA en Análisis con Ant Design X

**Input**: Design documents from `specs/025-rediseno-chat-analisis-antd-x/`

**Prerequisites**: plan.md, spec.md, research.md

## Phase 1: Setup

- [x] T001 Bump `antd` en `src/mpm-web/package.json` de `^5.18.0` a `^5.20.3` — sigue siendo v5, sin breaking changes esperados (ver research.md R2).
- [x] T002 Instalar `@ant-design/x@1.6.1` (NO la última versión absoluta — ver research.md R1, v2.x requiere antd 6) en `src/mpm-web/package.json`.
- [x] T003 `npm run build` en `src/mpm-web` tras el bump — compila sin errores (3765 modules, 11.48s).

## Phase 2: Foundational

*Ninguna tarea foundational — es un solo componente (`AnalisisChat.tsx`) sin dependencias de otras historias.*

---

## Phase 3: User Story 1 — Burbujas de conversación con componentes reales de chat (P1)

**Goal**: Reemplazar los divs manuales del historial de mensajes por `Bubble.List`/`Bubble`, preservando markdown, avatares, mensaje optimista e indicador de "escribiendo".

**Independent Test**: Abrir un análisis con historial existente, confirmar que los mensajes se ven con `Bubble.List`, que el markdown de las respuestas de la IA sigue renderizando bien, y que el indicador de "escribiendo" aparece mientras se espera respuesta.

- [x] T004 [US1] En `AnalisisChat.tsx`, reemplazar el `<Space direction="vertical">` con el `.map()` manual de mensajes por `Bubble.List`, mapeando `ChatMensaje[]` a la forma `items` que espera el componente (`key`, `role: msg.rol === 'user' ? 'user' : 'ai'`, `content`).
- [x] T005 [P] [US1] Migrar el `contentRender` de cada `Bubble` para reutilizar `normalizarMarkdown` + `ReactMarkdown` tal cual se usa hoy en el mensaje de la IA (sin reescribir el parsing de markdown).
- [x] T006 [P] [US1] Migrar los avatares (círculo rojo `#E30613` para usuario, círculo morado `#8b5cf6`/`#a78bfa` para IA con `UserOutlined`/`RobotOutlined`) al prop `avatar` de `Bubble`, preservando los mismos gradientes/colores que hoy.
- [x] T007 [US1] Reemplazar el div de 3 puntos (`mpm-typing-dot`) que se muestra mientras `chatMutation.isPending` por el prop `loading`/`typing` nativo de `Bubble`.
- [x] T008 [US1] Confirmar que el mensaje optimista del usuario (`pendingUserMsg`, aparece antes de que el servidor confirme) se sigue renderizando correctamente como un `Bubble` más en la lista.
- [x] T009 [US1] Preservar el estado vacío informativo ("Haz una pregunta sobre el análisis...") cuando `mensajes.length === 0`, adaptado al layout de `Bubble.List`.
- [x] T010 [US1] Preservar el `useEffect` de auto-scroll al fondo (`chatEndRef.current?.scrollIntoView`) — el centinela `div ref={chatEndRef}` se mantiene como respaldo al scroll nativo de `Bubble.List`.

**Checkpoint**: ✅ US1 completo — historial de chat renderizado con `Bubble.List`, funcionalmente idéntico a hoy.

---

## Phase 4: User Story 2 — Compositor de mensajes con `Sender` (P2)

**Goal**: Reemplazar `Input` + `Button` por `Sender`, preservando envío con Enter, guard de mensaje vacío, y estado de carga mientras se espera respuesta.

**Independent Test**: Escribir un mensaje, confirmar que Enter lo envía, que el compositor muestra su propio estado de carga mientras se espera la IA, y que vuelve a estar disponible cuando la respuesta llega.

- [x] T011 [US2] En `AnalisisChat.tsx`, reemplazar el `<Input>` + `<Button icon={<SendOutlined />}>` por `<Sender>`, conectando `value`/`onChange` al mismo estado `chatInput` que hoy.
- [x] T012 [US2] Conectar `onSubmit` de `Sender` a `handleEnviarChat` (mismo guard existente: `!workspaceId || !chatInput.trim()` retorna sin hacer nada).
- [x] T013 [P] [US2] Conectar el prop `loading` de `Sender` a `chatMutation.isPending`, reemplazando el `loading` que hoy tenía el `Button`.
- [x] T014 [P] [US2] Preservar el `placeholder` actual ("Pregunta sobre el análisis...") y el ícono `RobotOutlined` como prefijo (`prefix` prop de `Sender`).

**Checkpoint**: ✅ US2 completo — compositor funcional con `Sender`, mismo comportamiento de envío que hoy.

---

## Phase 5: User Story 3 — Consistencia visual con el resto de la app (P3)

**Goal**: Confirmar que la paleta de colores TIVIT (rojo usuario, morado IA) se mantiene con los componentes nuevos, sin colores default de Ant Design X.

**Independent Test**: Comparar visualmente el chat rediseñado contra otras superficies que ya usan el gradiente morado y confirmar que coinciden.

- [x] T015 [US3] Configurar los colores de `Bubble`/`Sender` via `styles` props para que coincidan con `#E30613` (usuario) y `#8b5cf6`/`#a78bfa` (IA) — definidos en `rolesConfig` objeto.
- [x] T016 [P] [US3] No se usa `XProvider` — se usan directamente los `styles` props de cada componente, sin conflicto con el `ConfigProvider` de Ant Design 5 ya configurado.
- [x] T017 [US3] Limpiar las clases CSS ya no usadas (`mpm-chat-user`, `mpm-chat-assistant`, `mpm-typing-dot`) del stylesheet global — eliminadas de `global.css` líneas 508-543.

**Checkpoint**: ✅ US3 completo — el chat rediseñado usa la paleta TIVIT, sin colores default de Ant Design X.

---

## Phase 6: Polish & Validación

- [x] T018 `npm run build` completo en `src/mpm-web` — ✅ 0 errores, 3765 módulos, 11.48s.
- [ ] T019 Probar en vivo (local, `docker compose` o `npm run dev`): abrir un análisis con historial existente, enviar un mensaje nuevo, confirmar respuesta con markdown, confirmar en las dos superficies (`/analisis/{id}/chat` y el dashboard embebido).
- [ ] T020 Comparación visual lado a lado (screenshot) del chat antes/después, para confirmar SC-001/SC-002 de spec.md.

## Dependencies

- **US1 (Fase 3)** no depende de US2/US3 — es el historial de mensajes, independiente del compositor.
- **US2 (Fase 4)** puede hacerse en paralelo a US1 (son partes distintas del mismo archivo, pero lógicamente independientes) o después, según preferencia de quien implemente.
- **US3 (Fase 5)** depende de que US1 y US2 ya usen los componentes de Ant Design X (no se puede "afinar colores" de algo que no existe todavía).

## Parallel Execution Examples

```text
# US1 y US2 tocan el mismo archivo (AnalisisChat.tsx) -- en la práctica conviene
# una sola persona/agente llevando ambas historias en secuencia dentro del archivo,
# no en paralelo real, para evitar conflictos de merge sobre el mismo componente.

# Dentro de US1, en paralelo entre sí:
T005 (markdown) ‖ T006 (avatares)

# Dentro de US2, en paralelo entre sí:
T013 (loading) ‖ T014 (placeholder/prefix)
```

## Implementation Strategy

**MVP = US1**, es la que más impacto visual tiene y motivó el pedido. US2 y US3 son incrementales sobre la misma base y de menor esfuerzo cada una (reusan el mismo archivo ya tocado). No hay razón para desplegar parcialmente — al ser un solo componente compartido, tiene sentido completar las 3 historias antes de un solo deploy a producción.
