# Contract: Seguimiento de Aclaraciones — Fase 4

**Feature**: MPM CU010 — Fase 4
**Date**: 2026-06-24

---

## Nuevos Endpoints REST

### Toggle seguimiento de licitación

```
POST /api/v1/licitaciones/{id}/seguir
Authorization: Bearer {jwt}
```

**Response 200** — acción ejecutada:
```json
{
  "success": true,
  "data": {
    "licitacionId": 42,
    "accion": "seguida"       // o "no_seguida"
  }
}
```

**Response 404** — licitación no existe:
```json
{ "success": false, "errors": [{ "message": "LIC_001: Licitación no encontrada" }] }
```

---

### Check si el usuario sigue una licitación

```
GET /api/v1/licitaciones/{id}/seguida
Authorization: Bearer {jwt}
```

**Response 200**:
```json
{
  "success": true,
  "data": { "esSeguida": true }
}
```

---

### Listar licitaciones que el usuario sigue

```
GET /api/v1/licitaciones/seguidas
Authorization: Bearer {jwt}
```

**Response 200**:
```json
{
  "success": true,
  "data": {
    "items": [
      {
        "licitacionId": 42,
        "codigoExterno": "1234-56-LP24",
        "nombre": "Suministro de hardware para datacenter",
        "codigoEstado": 1,
        "fechaCierre": "2026-07-15T23:59:00",
        "seguidaDesde": "2026-06-20T10:30:00"
      }
    ],
    "totalRecords": 3
  }
}
```

---

## Notificación tipo `aclaracion_detectada`

Generada por `AclaracionMonitorService` cuando detecta una nueva aclaración. Se crea via `NotificacionesService.CrearAsync()`.

### Estructura de metadata (JSONB):
```json
{
  "licitacion_id": 42,
  "codigo_externo": "1234-56-LP24",
  "codigo_aclaracion": 3,
  "tiene_respuesta": true
}
```

### Rendering en frontend:
- Ícono: `QuestionCircleOutlined` (color amarillo)
- Título: `"Nueva aclaración — {nombre_licitacion_corto}"`
- Mensaje: primeros 120 chars de la pregunta + `"..." si hay más`
- Link: `/licitaciones` con parámetro de búsqueda del código externo

---

## Ciclo del Monitor

```
AclaracionMonitorService
├── Startup: esperar 60s
├── Loop (cada MONITOR_INTERVAL_MINUTES=30):
│   ├── usp_Licitaciones_ObtenerParaMonitor([1,2,4])
│   │   → lista de {licitacion_id, codigo_externo, usuario_ids[]}
│   ├── Para cada licitación (con delay 1s entre requests):
│   │   ├── ApiMpService.GetDetalleAsync(codigo_externo, ticket)
│   │   ├── Si falla (429/timeout): log warning, continuar con la siguiente
│   │   └── Para cada pregunta en Preguntas.Listado:
│   │       ├── usp_Licitaciones_Aclaracion_Upsert(...)
│   │       └── Si p_es_nueva = TRUE:
│   │           └── Para cada usuario_id en usuario_ids[]:
│   │               NotificacionesService.CrearAsync(aclaracion_detectada)
│   └── Log: "Monitor cycle completed: {N} licitaciones, {M} notificaciones"
└── Error handler: log + continuar en próximo ciclo
```

---

## Variable de entorno nueva

| Variable | Descripción | Default |
|---|---|---|
| `MONITOR_INTERVAL_MINUTES` | Frecuencia del ciclo de monitoreo | `30` |
| `MONITOR_ENABLED` | Habilitar el servicio de monitoreo | `true` |

---

## Estados activos monitoreados

| Código | Descripción |
|---|---|
| 1 | Publicada |
| 2 | Cerrada (puede tener aclaraciones tardías) |
| 4 | Publicada con Preguntas |
