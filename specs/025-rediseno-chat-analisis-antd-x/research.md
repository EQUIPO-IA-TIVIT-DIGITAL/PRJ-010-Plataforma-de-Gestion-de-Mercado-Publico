# Research: Rediseño del chat de IA en Análisis con Ant Design X

## R1 — Versión de `@ant-design/x` a usar

**Decision**: Instalar `@ant-design/x@1.6.1` (última versión de la rama v1), NO la última versión absoluta (`2.8.0`).

**Rationale**: Verificado contra el registro de npm (`npm view @ant-design/x@<version> peerDependencies`):

| Rama | Última versión | Peer dep `antd` |
|---|---|---|
| v1 | 1.6.1 | `^5.20.3` |
| v2 | 2.8.0 | `^6.1.1` |

El proyecto usa `antd ^5.18.0` (ver `src/mpm-web/package.json`). Adoptar v2.x forzaría un upgrade de Ant Design 5→6 — un cambio mayor no pedido, con riesgo de romper el resto de la app (toda la UI existente usa antd 5). La rama v1 se queda dentro de antd 5.

## R2 — Antd 5.18.0 vs el peer dep `^5.20.3`

**Decision**: Subir `antd` a `^5.20.3` (o la última patch de la serie 5.x disponible) como parte de esta implementación, en vez de ignorar el mismatch.

**Rationale**: El Dockerfile de `mpm-web` ya usa `npm ci --legacy-peer-deps`, así que instalar `@ant-design/x@1.6.1` sobre `antd@5.18.0` no rompería el build — pero depender de eso es frágil (un futuro `npm install` sin ese flag, o una herramienta que sí valide peer deps estrictamente, fallaría). Subir de 5.18 a 5.20+ es un bump menor dentro de la misma major version, bajo riesgo de romper componentes existentes (no es un cambio de v5 a v6).

**Alternativa considerada**: Dejar `antd` en 5.18.0 y confiar en `--legacy-peer-deps` — descartado, es una deuda técnica innecesaria cuando el fix real es trivial (bump de patch/minor).

## R3 — Componentes relevantes de `@ant-design/x` para este caso de uso

**Decision**: Usar `Bubble.List` (historial) + `Bubble` (mensaje individual) + `Sender` (compositor). No se necesita `useXChat`/`useXAgent`/`XStream` porque el backend (`GeminiService`) responde de forma no-streaming (una sola respuesta bloqueante) — esos hooks están pensados para conectar a un backend que emite tokens progresivamente (SSE/streaming), que no es el caso hoy.

**Mapeo con la implementación actual** (`AnalisisChat.tsx`):

| Hoy (manual) | Ant Design X |
|---|---|
| `<div>` por mensaje + avatar armado a mano | `Bubble` (prop `avatar`, `placement`, `content`) |
| Lista de mensajes con `Space direction="vertical"` | `Bubble.List` (prop `items`, `role`) |
| 3 puntos CSS (`mpm-typing-dot`) mientras `chatMutation.isPending` | `Bubble` con `loading`/`typing` |
| `ReactMarkdown` + `normalizarMarkdown` | Se preservan igual, dentro de `contentRender` de `Bubble` |
| `Input` + `Button` con `loading` | `Sender` (props `value`, `onSubmit`, `loading`) |
| Clases CSS `mpm-chat-user`/`mpm-chat-assistant` (rojo/morado TIVIT) | Se configuran como `variant`/estilos por `role` en `Bubble.List`, o via CSS vars existentes reutilizadas |

**Alternativas consideradas**: Escribir el redisño sin la librería (solo ajustar CSS de lo existente) — descartado porque el pedido explícito fue adoptar Ant Design X, no solo un retoque visual.

## R4 — Alcance de la migración

**Decision**: Esta spec cubre únicamente `AnalisisChat.tsx` (el componente compartido por `/analisis/{id}/chat` y el dashboard embebido). No toca `AnalisisController`, `GeminiService`, ni el modelo de datos de conversaciones.

**Nota relacionada, fuera de alcance**: la conversación de chat es por `workspace_id` sin `user_id` (confirmado en vivo 2026-07-10) — dos usuarios distintos ven y comparten el mismo hilo. Si se decide que debe ser privado por usuario, es un cambio de modelo de datos que amerita su propia spec.
