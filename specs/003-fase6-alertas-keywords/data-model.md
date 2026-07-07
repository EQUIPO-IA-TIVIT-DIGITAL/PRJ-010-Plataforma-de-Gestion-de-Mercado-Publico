# Data Model: Fase 6 — Alertas Inteligentes por Palabras Clave

**Spec**: [spec.md](./spec.md) | **Research**: [research.md](./research.md)
**Migración**: `V079__Create_Alertas.sql` (V078 ya usada por `016-extraccion-documentos-api`)

## Entidades

### `alertas_reglas`

Una regla de alerta configurada por un usuario.

| Columna | Tipo | Notas |
|---|---|---|
| `id` | BIGSERIAL PK | |
| `usuario_id` | UUID/TEXT | dueño de la regla (creador) |
| `keyword` | VARCHAR(200) | término literal ingresado por el usuario |
| `sinonimos_ia` | JSONB | array de sinónimos/conceptos generados por IA al crear/editar (research.md §2), null si aún no calculado |
| `monto_minimo` | NUMERIC | opcional |
| `monto_maximo` | NUMERIC | opcional |
| `tipos_licitacion` | INT[] | opcional, códigos de tipo de licitación |
| `organismos` | TEXT[] | opcional |
| `activa` | BOOLEAN | default true |
| `notificar_telegram` | BOOLEAN | default false — si el usuario activó el canal Telegram para esta regla |
| `created_at` / `updated_at` | TIMESTAMP | |
| `record_status` | SMALLINT | soft delete, convención del proyecto |

### `alertas_disparadas`

Un registro de que una licitación disparó una regla (para deduplicación e historial).

| Columna | Tipo | Notas |
|---|---|---|
| `id` | BIGSERIAL PK | |
| `regla_id` | BIGINT FK → `alertas_reglas` | |
| `licitacion_id` | BIGINT FK → `licitaciones` | |
| `termino_match` | VARCHAR(200) | la keyword o el sinónimo específico que disparó el match (trazabilidad, User Story 3) |
| `resumen_enriquecido` | JSONB | `{ requisitos, competidores, presupuesto, forma_pago, multas, es_renovacion, proveedor_actual }` — campos ausentes se guardan como `null`, nunca inventados (spec, User Story 4 escenario 3) |
| `notificacion_inapp_id` | BIGINT | FK opcional a la notificación creada en `MPM.Modules.Notificaciones` |
| `notificacion_telegram_enviada` | BOOLEAN | default false |
| `notificacion_telegram_error` | TEXT | último error si el envío a Telegram falló (no bloqueante) |
| `es_prueba` | BOOLEAN | default false — true si vino del endpoint de "disparar alerta de prueba" (research.md §5) |
| `disparada_en` | TIMESTAMP | default now |

Índice único `(regla_id, licitacion_id)` para deduplicación (una licitación no dispara la misma regla dos veces).

### `alertas_destinatarios`

Mapeo de usuarios a su `chat_id` de Telegram y si son account manager de gobierno (destinatarios de la notificación enriquecida, User Story 4).

| Columna | Tipo | Notas |
|---|---|---|
| `id` | BIGSERIAL PK | |
| `usuario_id` | UUID/TEXT | único |
| `telegram_chat_id` | VARCHAR(50) | nullable — se completa cuando el usuario inicia conversación con el bot (research.md §4) |
| `es_account_manager_gobierno` | BOOLEAN | default false |
| `created_at` / `updated_at` | TIMESTAMP | |

## Relaciones

```
usuarios (Auth, externo) ─┬─< alertas_reglas
                          └─< alertas_destinatarios

alertas_reglas ─< alertas_disparadas >─ licitaciones (existente)
```

## Reglas de validación

- `keyword` no vacío, mínimo 2 caracteres.
- Al menos uno de `keyword`/`monto_minimo`/`monto_maximo`/`tipos_licitacion`/`organismos` debe estar presente (una regla no puede estar completamente vacía).
- `monto_minimo <= monto_maximo` cuando ambos están presentes.
- `resumen_enriquecido`: cada campo individual es nullable; el objeto completo nunca se omite (siempre se genera, aunque sea con todos los campos en `null`).

## Sin cambios en `licitaciones`, `licitaciones_adjuntos`, `notificaciones` existentes — solo lectura/integración.
