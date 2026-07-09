# Data Model: Inteligencia de competencia, alertas interactivas y canal de correo

## `licitaciones_ofertas` (NUEVA, V097)

| Columna | Tipo | Notas |
|---|---|---|
| `id` | `BIGSERIAL PRIMARY KEY` | |
| `licitacion_id` | `BIGINT NOT NULL REFERENCES licitaciones(id)` | |
| `rut_proveedor` | `VARCHAR(20)` | tal como viene del Cuadro de Ofertas |
| `nombre_proveedor` | `VARCHAR(300) NOT NULL` | usado para la búsqueda por competidor (R4) |
| `monto_oferta` | `NUMERIC(18,2)` | nullable — puede no venir en todos los casos |
| `estado_oferta` | `VARCHAR(30)` | `'Aceptada'`, `'Rechazada'`, u otros valores tal como los expone Mercado Público |
| `created_at` | `TIMESTAMP DEFAULT CURRENT_TIMESTAMP` | |
| `updated_at` | `TIMESTAMP DEFAULT CURRENT_TIMESTAMP` | |

Índice: `UNIQUE (licitacion_id, rut_proveedor)` (evita duplicar la misma oferta si el scraper revisita la licitación); índice `pg_trgm` sobre `nombre_proveedor` para la búsqueda de competidor (R4).

## `competidores_analisis` (NUEVA, V098)

| Columna | Tipo | Notas |
|---|---|---|
| `id` | `BIGSERIAL PRIMARY KEY` | |
| `nombre_competidor` | `VARCHAR(300) NOT NULL` | tal como lo escribió el usuario al pedir el análisis |
| `fecha_desde` | `DATE NOT NULL` | |
| `fecha_hasta` | `DATE NOT NULL` | |
| `contenido_json` | `JSONB NOT NULL` | resultado del análisis de Gemini (patrones, organismos, montos, recomendaciones) |
| `cantidad_licitaciones` | `INT NOT NULL` | cuántas ofertas entraron en el análisis (para mostrar el volumen, FR-006, y como contexto del resultado guardado) |
| `creado_por_usuario_id` | `VARCHAR(100) NOT NULL` | |
| `created_at` | `TIMESTAMP DEFAULT CURRENT_TIMESTAMP` | |

Índice: `UNIQUE (nombre_competidor, fecha_desde, fecha_hasta)` — implementa directamente la clave de caché de R5; un `INSERT ... ON CONFLICT DO NOTHING` más un `SELECT` previo resuelve FR-005 sin necesitar lógica adicional de aplicación para la concurrencia (Edge Case de dos usuarios pidiendo el mismo análisis a la vez).

## `alertas_destinatarios` (EXISTENTE, se extiende en V099)

| Columna | Tipo | Notas |
|---|---|---|
| ...columnas existentes... | | sin cambios (`usuario_id`, `telegram_chat_id`, `es_account_manager_gobierno`) |
| `email_alertas` | `VARCHAR(200)` | **NUEVA** — nullable, dirección de correo configurada por el usuario para recibir alertas |

No se crea una tabla separada para el canal de correo — es una extensión directa de la misma tabla que ya modela "a dónde le mando las alertas a este usuario", igual que ya hace `telegram_chat_id`.

## Cambios a entidades existentes

- **Mensaje de alerta de Telegram** (`TelegramNotificationService.FormatearMensaje`): se agrega un `reply_markup` con un botón inline `{"text": "Me interesa", "callback_data": "interesa:<licitacionId>"}` al `sendMessage` — no cambia el texto del mensaje en sí.
- **`TelegramWebhookController`**: el payload de un `callback_query` de Telegram trae `callback_query.data` y `callback_query.message.chat.id` — se parsea `data` para extraer el `licitacionId`, se llama `ApiMpService.GetDetalleAsync`, y se responde con `sendMessage` al mismo chat.
