# Quickstart: Validar Mejora de Alertas por Correo

## Prerrequisitos

- Stack local en Docker corriendo (`docker compose up -d`)
- Usuario con al menos una alerta configurable (`admin@tivit.cl` / `test123` en dev)
- Endpoint `POST /api/v1/alertas/{id}/probar` disponible (ya existe, usado por el botón de prueba de alertas)

## US1 — Matching sin falsos positivos

1. Crear una alerta con keyword `"TI"` vía `POST /api/v1/alertas`.
2. Ejecutar el matching de prueba contra una licitación de fixture con nombre `"Producción evento mujeres participantes"` (usar `ProbarAsync`/`EvaluarMatch` con `forzarMatch: false` para que sí aplique el filtro real, no el de prueba forzada).
3. **Esperado**: sin match (retorna `null`).
4. Repetir contra una licitación con nombre `"Servicio de soporte TI para oficinas regionales"`.
5. **Esperado**: match, término devuelto = `"TI"`.
6. Repetir con una alerta de keyword compuesta `"mesa de ayuda"` contra una licitación que contenga esa frase completa.
7. **Esperado**: sigue matcheando igual que antes del cambio (no regresión de FR-002).

## US2 — Correo enriquecido

1. Disparar `POST /api/v1/alertas/{id}/probar` sobre una licitación real con organismo, fecha de cierre y link poblados.
2. Revisar el correo recibido (o el HTML devuelto si el endpoint de prueba lo expone) contra el formato fijado en `contracts/correo-alerta-formato.md`.
3. **Esperado**: aparecen organismo, fecha de cierre y enlace, además de lo que ya mostraba antes.
4. Repetir contra una licitación sin fecha de cierre informada.
5. **Esperado**: el correo se genera igual, sin esa línea, sin texto roto.

## US3 — Horario del disparador

1. Tras aplicar el cambio en Cloud Scheduler:
   ```bash
   gcloud scheduler jobs describe sync-job-scheduler --project=tivit-cu010 --location=us-central1 --format="value(schedule)"
   ```
2. **Esperado**: devuelve `0 8,15 * * *`.
3. No requiere esperar a un disparo real para validar — es un cambio de configuración, no de código.
