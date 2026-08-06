# Contrato: Flujo colaborativo go/no-go (US5)

Ver `research.md` §5: la orquestación de los 3 pasos (análisis, conversación, vínculo) ocurre en el frontend llamando a 3 endpoints independientes — sin acoplamiento nuevo entre módulos backend.

## Paso 1 — `POST /api/v1/licitaciones/{licitacionId}/interes` (nuevo, módulo `Colaboracion`)

Marca la licitación como "de interés". Idempotente: si ya existe una fila en `licitaciones_interes` para esa licitación, la devuelve tal cual (no crea una segunda).

```json
// Response 200/201
{
  "id": 12,
  "licitacionId": 1204,
  "workspaceId": null,
  "conversacionId": null,
  "marcadoPor": "francisco.lopez",
  "estadoLicitacionAlMarcar": 8,
  "createdAt": "2026-08-05T10:00:00Z"
}
```

## Paso 2 — el frontend dispara el análisis y crea la conversación (endpoints existentes)

- `POST /api/v1/analisis/workspaces` (o el endpoint idempotente equivalente que ya exista — **confirmar firma exacta en `tasks.md`**, ver nota en `research.md` §5) con `licitacionId`. Reutiliza el análisis ya generado si existe (FR-013).
- `POST /api/v1/mensajeria/conversaciones` (endpoint ya existente) con `tipo: "grupal"`, `licitacionId: 1204`, `asunto` auto-completado con el nombre de la licitación.

## Paso 3 — `PATCH /api/v1/licitaciones/{licitacionId}/interes/vincular` (nuevo, módulo `Colaboracion`)

El frontend persiste los IDs obtenidos en el paso 2:

```json
// Request
{ "workspaceId": 84, "conversacionId": 231 }
```

Una vez vinculados ambos, `GET /api/v1/licitaciones/{licitacionId}/interes` expone el paquete completo para la UI (licitación + análisis + conversación) en una sola llamada de lectura.

## Asignar trabajadores y comentar (endpoints existentes de Mensajería, sin cambio)

- `POST /api/v1/mensajeria/conversaciones/{conversacionId}/participantes` — agrega usuarios asignados (ya existe, vía `conversacion_participantes`).
- `POST /api/v1/mensajeria/conversaciones/{conversacionId}/mensajes` — comentarios internos, visibles entre asignados, con autor/fecha y push en tiempo real (ya existe, vía `mensajes` + SignalR).

## `GET /api/v1/licitaciones/interes` (nuevo, módulo `Colaboracion`)

Lista las licitaciones de interés del tenant (para una futura vista "Mis licitaciones asignadas" — no es una historia de usuario explícita del spec, pero es el listado natural detrás de FR-014/FR-015; se documenta aquí para que `tasks.md` no lo omita por descuido).

## Edge cases cubiertos por este contrato

- **FR-013 (no duplicar análisis)**: garantizado a nivel de esquema por `UNIQUE(licitacion_id)` en `licitaciones_interes` (ver `data-model.md`) — un segundo `POST /interes` sobre la misma licitación no crea una segunda fila ni dispara un segundo análisis.
- **FR-017 (cambio de estado tras marcar)**: `estado_licitacion_al_marcar` se compara en lectura contra `licitaciones.codigo_estado` actual; si difieren, el frontend muestra el aviso visual.
- **FR-018 (comentarios de un usuario que pierde acceso)**: no requiere nada nuevo — `mensajes.user_id` es un valor de texto histórico, no un FK con `ON DELETE CASCADE`, así que los comentarios sobreviven aunque el usuario se desactive en `usuarios` (confirmar este comportamiento contra el esquema real de `usuarios`/`mensajes` en `tasks.md`, no se verificó línea por línea en esta ronda de research).
