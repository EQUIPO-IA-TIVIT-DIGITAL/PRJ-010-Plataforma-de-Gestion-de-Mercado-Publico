# Feature Specification: Rediseño del chat de IA en Análisis con Ant Design X

**Feature Branch**: `025-rediseno-chat-analisis-antd-x`

**Created**: 2026-07-10

**Status**: Draft

**Input**: User description: "El chat de IA en /analisis es funcional pero la estética es genérica (divs a mano, sin componentes reales de chat). Adoptar Ant Design X (https://x.ant.design/) para darle la estética correcta de una interfaz de chat con IA, manteniendo el diseño consistente con el resto de la app (que ya usa Ant Design 5)."

## Contexto — qué existe hoy

`AnalisisChat.tsx` (usado por `AnalisisChatPage.tsx` y embebido en el dashboard) es una implementación 100% manual: divs con estilos inline para cada burbuja, avatares circulares armados a mano, un indicador de "escribiendo" con 3 puntos CSS, `react-markdown` para las respuestas de la IA, y un `Input` + `Button` de Ant Design 5 como compositor. Funciona (verificado en vivo el 2026-07-10 contra producción real), pero no usa ningún componente dedicado a chat — es la razón por la que "se ve genérico".

El backend (`AnalisisController.Chat`, `GeminiService`) responde de forma **no-streaming**: una sola llamada bloqueante a Vertex AI que devuelve la respuesta completa. Esta spec no cambia esa arquitectura — es un rediseño de la capa de presentación, no de la lógica de negocio ni del contrato de la API.

**Fuera de alcance de esta spec** (anotado para una spec futura, no se resuelve acá): hoy la conversación de chat es por `workspace_id`, no por usuario (`analisis_chat_conversaciones` no tiene columna `user_id`) — dos personas distintas viendo el mismo análisis comparten el mismo hilo. Confirmado en vivo el 2026-07-10. Si se decide que cada usuario debe tener su propia conversación privada, es un cambio de modelo de datos que se aborda por separado.

## User Scenarios & Testing *(mandatory)*

### User Story 1 - Burbujas de conversación con componentes reales de chat (Priority: P1)

Un usuario abre el chat de un análisis y ve los mensajes (suyos y de la IA) renderizados con `Bubble`/`Bubble.List` de Ant Design X en vez de divs armados a mano — mismo contenido y comportamiento de hoy (avatares diferenciados usuario/IA, markdown en las respuestas, orden cronológico, scroll automático al fondo), pero con la estética y micro-interacciones propias de un componente de chat de IA real (variant/shape consistentes, transición de "escribiendo" nativa vía `loading`).

**Why this priority**: Es el cambio que más se nota visualmente y el que motivó el pedido — sin esto, todo lo demás es reordenar el mismo look genérico.

**Independent Test**: Abrir un análisis con historial de chat existente, confirmar que los mensajes se ven con el nuevo componente, que las respuestas de la IA siguen renderizando markdown correctamente (listas, negritas, código si aplica), y que el mensaje optimista del usuario y el indicador de "escribiendo" de la IA se comportan igual que hoy (aparecen al instante, sin esperar la respuesta del servidor).

**Acceptance Scenarios**:

1. **Given** un workspace con historial de chat existente, **When** el usuario abre `/analisis/{id}/chat`, **Then** ve los mensajes previos renderizados con `Bubble.List`, con la misma información (contenido, quién lo escribió, hora) que la implementación actual.
2. **Given** una respuesta de la IA con formato markdown (listas, negritas, títulos), **When** se renderiza en el nuevo `Bubble`, **Then** el formato se ve igual de bien o mejor que con `react-markdown` hoy (sin perder el `normalizarMarkdown` que limpia fences envolventes).
3. **Given** que el usuario envía un mensaje, **When** la IA todavía no responde, **Then** se ve un indicador de "escribiendo" usando el prop `loading`/`typing` de `Bubble` en vez del div con 3 puntos CSS actual.
4. **Given** un workspace sin ningún mensaje todavía, **When** el usuario abre el chat, **Then** ve el mismo estado vacío informativo que hoy ("Haz una pregunta sobre el análisis..."), adaptado al layout nuevo.

---

### User Story 2 - Compositor de mensajes con `Sender` (Priority: P2)

El campo para escribir preguntas usa el componente `Sender` de Ant Design X en vez del `Input` + `Button` genéricos de hoy — mismo comportamiento (Enter para enviar, deshabilitado mientras la IA responde, mismo placeholder), con la apariencia y ergonomía nativas de un compositor de chat de IA (auto-resize, estado de carga integrado en el botón de envío).

**Why this priority**: Complementa a US1 — el compositor es la otra mitad de la superficie de chat visible todo el tiempo, pero tiene menos impacto visual por sí solo que el historial de mensajes.

**Independent Test**: Escribir un mensaje, confirmar que Enter lo envía, que el compositor se deshabilita/muestra estado de carga mientras se espera la respuesta, y que vuelve a estar disponible apenas la respuesta llega.

**Acceptance Scenarios**:

1. **Given** el chat de un análisis, **When** el usuario escribe una pregunta y presiona Enter (o el botón de enviar), **Then** el mensaje se envía igual que hoy, usando `Sender` en vez de `Input`+`Button`.
2. **Given** que se envió un mensaje, **When** la IA está generando la respuesta, **Then** el `Sender` muestra su propio estado de carga (en vez del `Button` con `loading` actual) y no permite enviar un segundo mensaje hasta que la respuesta llegue.
3. **Given** el compositor vacío, **When** el usuario presiona enviar sin escribir nada, **Then** no se envía ninguna request (mismo guard que hoy: `!chatInput.trim()`).

---

### User Story 3 - Consistencia visual con el resto de la app (Priority: P3)

Los colores, tipografía y espaciado del chat rediseñado siguen la misma paleta que ya usa el resto de MPM (rojo TIVIT para el usuario, morado para la IA — los mismos gradientes que hoy usa `AnalisisChat.tsx`), usando el mecanismo de theming de Ant Design X (`XProvider` o tokens de `ConfigProvider` ya existentes) en vez de clases CSS sueltas (`mpm-chat-user`, `mpm-chat-assistant`).

**Why this priority**: Sin esto, el chat rediseñado podría verse "bien" pero desentonar del resto de la aplicación — es un requisito explícito del pedido original ("manteniendo el diseño consistente con lo demás").

**Independent Test**: Comparar visualmente el chat rediseñado contra otras superficies de la app (ej. el botón "Analizar con IA" en Competidores, que ya usa el gradiente morado) y confirmar que los colores coinciden, no son una paleta nueva.

**Acceptance Scenarios**:

1. **Given** el chat rediseñado, **When** se compara con el resto de la UI, **Then** usa los mismos colores ya establecidos (rojo TIVIT `#E30613` para el usuario, morado `#8b5cf6`/`#a78bfa` para la IA) en vez de los colores default de Ant Design X.
2. **Given** el `ConfigProvider` ya configurado a nivel de la app (si existe un theme custom), **When** se agrega `XProvider`, **Then** no rompe ni duplica configuración de tema existente.

### Edge Cases

- **Compatibilidad de versión — YA INVESTIGADO (ver research.md)**: `@ant-design/x` v2.x requiere `antd ^6.1.1` (breaking change mayor, fuera de alcance). La rama v1 (última: `1.6.1`) requiere `antd ^5.20.3` — compatible en espíritu con los `antd ^5.18.0` del proyecto (ambos v5), aunque técnicamente por debajo del mínimo declarado. El Dockerfile de `mpm-web` ya instala con `--legacy-peer-deps`, así que esto no bloquea el build, pero como parte de la implementación se sube `antd` a `^5.20.3` o superior (sigue siendo v5, no v6) para no depender de ignorar el peer dependency.
- ¿Qué pasa con el `contentRender` de `Bubble` y el markdown existente (`normalizarMarkdown` + `react-markdown`)? → Se reutiliza tal cual dentro de `contentRender`, no se reescribe el parsing de markdown en esta spec.
- ¿El mensaje optimista del usuario (aparece antes de que el servidor confirme) sigue funcionando igual? → Sí, es lógica de estado de React (`pendingUserMsg`) independiente del componente de presentación — se preserva sin cambios.

## Requirements *(mandatory)*

- **FR-001**: El sistema DEBE renderizar el historial de mensajes del chat usando `Bubble.List` de `@ant-design/x`, preservando el contenido, autor y timestamp de cada mensaje que se muestra hoy.
- **FR-002**: El sistema DEBE seguir renderizando las respuestas de la IA como markdown (reutilizando `normalizarMarkdown`), ahora dentro del mecanismo de contenido de `Bubble`.
- **FR-003**: El sistema DEBE mostrar el estado "escribiendo" de la IA usando las props nativas de `Bubble` (`loading`/`typing`), no un indicador armado a mano.
- **FR-004**: El sistema DEBE usar `Sender` de `@ant-design/x` como compositor de mensajes, preservando el envío con Enter y el guard de mensaje vacío.
- **FR-005**: El sistema NO DEBE cambiar el contrato de la API de chat (`useEnviarChat`, `useChatHistorial`) ni la arquitectura no-streaming del backend — es un cambio de presentación únicamente.
- **FR-006**: El sistema DEBE mantener la paleta de colores ya usada por TIVIT (rojo `#E30613` usuario, morado `#8b5cf6`/`#a78bfa` IA) al configurar los componentes de Ant Design X.
- **FR-007**: El sistema DEBE seguir funcionando igual en las dos superficies donde se usa `AnalisisChat` hoy: la página dedicada (`/analisis/{id}/chat`) y el embebido dentro del dashboard.

## Success Criteria *(mandatory)*

- **SC-001**: Un usuario que ya usaba el chat no nota pérdida de funcionalidad — mismo historial, mismo markdown, mismo comportamiento de envío — solo una estética distinta.
- **SC-002**: El chat rediseñado es visualmente indistinguible en paleta de color del resto de la aplicación (no parece "pegado" de otra librería).
- **SC-003**: `npm run build` de `mpm-web` sigue pasando sin errores tras agregar la dependencia nueva.
