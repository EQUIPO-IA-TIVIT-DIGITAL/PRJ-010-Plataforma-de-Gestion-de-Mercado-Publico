# Contracts: API de Alertas

**Feature**: [../spec.md](../spec.md) | **Data model**: [../data-model.md](../data-model.md)

Todos los endpoints bajo `/api/v1/alertas`, protegidos por JWT (igual que el resto de la API), con `TenantContext` inyectado (Principio IV).

## `GET /api/v1/alertas`

Lista las reglas del usuario autenticado.

**Response 200**:
```json
{
  "items": [
    {
      "id": 1,
      "keyword": "SOC",
      "sinonimosIa": ["centro de operaciones de seguridad", "monitoreo 24/7", "..."],
      "montoMinimo": 10000000,
      "montoMaximo": null,
      "tiposLicitacion": [],
      "organismos": [],
      "activa": true,
      "notificarTelegram": true
    }
  ]
}
```

## `POST /api/v1/alertas`

Crea una regla. Dispara la expansión de sinónimos vía IA de forma síncrona (research.md §2) antes de responder — el usuario ve los sinónimos generados al guardar.

**Request**:
```json
{ "keyword": "SOC", "montoMinimo": 10000000, "notificarTelegram": true }
```

**Response 201**: la regla creada, incluyendo `sinonimosIa` ya poblado.

## `PUT /api/v1/alertas/{id}`

Edita una regla existente. Si `keyword` cambia, recalcula `sinonimosIa`.

## `PATCH /api/v1/alertas/{id}/toggle`

Activa/pausa una regla sin perder su configuración (User Story 2).

## `DELETE /api/v1/alertas/{id}`

Soft delete (`record_status`).

## `GET /api/v1/alertas/{id}/historial`

Lista las `alertas_disparadas` de esa regla (paginado), para el panel de historial.

## `POST /api/v1/alertas/{id}/probar`

Dispara el pipeline completo (matching→enriquecimiento→notificación in-app + Telegram) contra una licitación real elegida por el usuario, para demo (research.md §5).

**Request**:
```json
{ "licitacionId": 12345 }
```

**Response 200**:
```json
{
  "alertaDisparadaId": 999,
  "esPrueba": true,
  "notificacionInAppCreada": true,
  "notificacionTelegramEnviada": true,
  "notificacionTelegramError": null
}
```

## `POST /api/v1/alertas/destinatarios/telegram/chat-id`

Endpoint de soporte para vincular el `telegram_chat_id` de un usuario (el usuario le escribe al bot, el bot recibe el `chat_id` vía webhook o polling, y este endpoint permite confirmarlo/guardarlo manualmente si el flujo automático no está disponible aún — ver quickstart.md para el procedimiento manual).

## Sin cambios de contrato en endpoints existentes

`POST /api/v1/licitaciones/sync` no cambia — el motor de matching de Alertas se invoca desde dentro del ciclo de sync (mismo proceso), no es un endpoint nuevo que alguien más llame.
