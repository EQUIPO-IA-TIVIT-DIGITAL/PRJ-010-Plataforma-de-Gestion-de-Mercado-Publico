# Quickstart: Validación del Seguimiento de Aclaraciones (Fase 4)

**Feature**: MPM CU010 — Fase 4: Notificaciones y Seguimiento Activo
**Date**: 2026-06-24

---

## Prerequisitos

1. Docker stack levantado (`docker compose up -d`)
2. Al menos 1 licitación en estado activo (código 1, 2 o 4) en la BD
3. `MP_TICKET` configurado en `.env` para el API de Mercado Público
4. `MONITOR_ENABLED=true` y `MONITOR_INTERVAL_MINUTES=30` en `.env`

---

## Setup

```bash
# Verificar que el stack está corriendo
docker compose ps

# Verificar variables del monitor
cat .env | grep -E "MONITOR_|MP_TICKET"

# Rebuild con los cambios de Fase 4
docker compose up --build -d
```

---

## Escenario 1: Marcar una licitación como seguida desde el frontend

```bash
# 1. Abrir http://localhost:8181
# 2. Login: admin@tivit.cl / test123
# 3. Ir a /licitaciones
# 4. Buscar una licitación activa (estado: Publicada)
# 5. Hacer clic en el ícono estrella (☆) en esa fila
# Resultado esperado: estrella se llena (★), badge de notificaciones aparece
#   o la licitación aparece destacada

# Verificar en BD:
docker compose exec db psql -U mpm -c \
  "SELECT l.codigo_externo, ls.usuario_id, ls.created_at
   FROM licitaciones_seguidas ls
   JOIN licitaciones l ON l.id = ls.licitacion_id
   ORDER BY ls.created_at DESC LIMIT 5;"
```

---

## Escenario 2: Verificar que el Monitor detecta aclaraciones

```bash
# 1. Verificar que el servicio de monitor arrancó
docker compose logs api | grep -i "AclaracionMonitor"
# Resultado esperado:
# "AclaracionMonitorService starting. Interval: 30m"
# "Monitor cycle triggered at ..."
# "Monitor cycle completed: N licitaciones, M notificaciones enviadas"

# 2. Simular detección manual (forzar un ciclo):
# El servicio corre cada 30 min — para pruebas, bajar MONITOR_INTERVAL_MINUTES=1
# o disparar una aclaración en la BD manualmente:
docker compose exec db psql -U mpm -c \
  "INSERT INTO licitaciones_aclaraciones
   (licitacion_id, codigo_aclaracion, pregunta, fecha_publicacion, notificado)
   SELECT id, 999, 'Pregunta de prueba desde QA', NOW(), false
   FROM licitaciones WHERE codigo_estado IN (1,2,4) LIMIT 1;"

# Luego esperar el próximo ciclo o reiniciar el servicio
# Verificar que se creó la notificación:
docker compose exec db psql -U mpm -c \
  "SELECT tipo, titulo, leido, created_at
   FROM notificaciones
   WHERE tipo = 'aclaracion_detectada'
   ORDER BY created_at DESC LIMIT 5;"
```

---

## Escenario 3: Verificar campana de notificaciones en frontend

```bash
# 1. Abrir http://localhost:8181
# 2. Login: admin@tivit.cl / test123
# 3. Verificar que la campana (🔔) en el header muestra badge con número
# 4. Hacer clic en la campana → debe aparecer la notificación de aclaración
# 5. Hacer clic en la notificación → debe llevar a la licitación correspondiente
# 6. Ir a /notificaciones → ver historial con la nueva notificación
```

---

## Escenario 4: Verificar API de seguimiento

```bash
# Obtener token JWT
TOKEN=$(curl -s -X POST http://localhost:5001/api/v1/auth/login \
  -H "Content-Type: application/json" \
  -d '{"email":"admin@tivit.cl","password":"test123"}' | jq -r '.data.token')

# Seguir una licitación (ID 1)
curl -s -X POST http://localhost:5001/api/v1/licitaciones/1/seguir \
  -H "Authorization: Bearer $TOKEN" | jq .

# Verificar que es seguida
curl -s http://localhost:5001/api/v1/licitaciones/1/seguida \
  -H "Authorization: Bearer $TOKEN" | jq .

# Listar todas las seguidas
curl -s http://localhost:5001/api/v1/licitaciones/seguidas \
  -H "Authorization: Bearer $TOKEN" | jq .

# Dejar de seguir (toggle)
curl -s -X POST http://localhost:5001/api/v1/licitaciones/1/seguir \
  -H "Authorization: Bearer $TOKEN" | jq .
# Resultado: { "accion": "no_seguida" }
```

---

## Troubleshooting

| Síntoma | Causa probable | Solución |
|---|---|---|
| `AclaracionMonitorService starting` no aparece en logs | `MONITOR_ENABLED=false` o no registrado | Verificar `.env` y `ModuleRegistration.cs` |
| Monitor corre pero no genera notificaciones | MP API no devuelve `Preguntas` para esas licitaciones | Normal si las licitaciones no tienen aclaraciones en MP; verificar `ApiMpLicitacion.Preguntas` en logs de debug |
| Error 429 en logs del monitor | Rate limit de MP API | El monitor hace pause de 1s entre requests — si persiste, aumentar delay |
| Notificación creada pero no aparece en campana | Polling interval de 30s en bell | Esperar máx 30s o recargar la página |
| Error al hacer seguir: 404 | `licitacion_id` no existe en BD | Asegurarse de que la licitación fue sincronizada primero |
