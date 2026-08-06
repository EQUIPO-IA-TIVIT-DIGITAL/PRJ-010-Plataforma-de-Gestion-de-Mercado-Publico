# Quickstart de validación: Feedback ChileCompra (031)

Prerequisito: `docker compose up --build` (API `:5001`, Web `:8181`, DB `:5433`) con las migraciones V118-V122 aplicadas.

## US1 — Filtro por área de negocio

1. `GET /api/v1/catalogos/areas-negocio` → confirma que devuelve las 3 áreas semilla.
2. `GET /api/v1/licitaciones?area=2&pageSize=20` (Ciberseguridad) → confirma que el total de resultados es una fracción pequeña del total sin filtrar, y que el `nombre`/`descripcion` de una muestra de resultados tiene relación real con ciberseguridad.
3. `GET /api/v1/licitaciones?sinClasificar=true&pageSize=20` → confirma que devuelve licitaciones (ninguna licitación real desaparece del sistema, FR-003).

## US2 — Estadísticas por estado + drill-down

1. `GET /api/v1/licitaciones/estadisticas-estado` → confirma que aparecen los 5 estados reales (5/6/7/8/15) con conteo, y que la suma de los 5 conteos es igual al total de `GET /api/v1/licitaciones` sin filtrar.
2. Tomar el `codigoEstado` de "Desierta" (7) y llamar `GET /api/v1/licitaciones?estado=7` → confirma que el conteo de resultados coincide con el número mostrado en el paso 1.
3. Repetir el paso 1 con `?area=1` → confirma que los conteos bajan y siguen sumando el total filtrado por área.

## US3 — Orden del historial de análisis

1. Crear (o usar) dos workspaces de análisis: uno cuya licitación tenga `fecha_adjudicacion` antigua (ej. febrero) generado hoy, y otro cuya licitación tenga `fecha_adjudicacion` reciente generado hace tiempo.
2. `GET /api/v1/analisis/workspaces` → confirma que el de licitación más reciente aparece primero, sin importar cuál `createdAt` es mayor.
3. Confirmar que el campo `fechaAdjudicacion` viaja en la respuesta de cada item.

## US4 — Actividad total de mercado de un competidor

1. `GET /api/v1/competidores/{nombreCompetidorConocido}/actividad-mercado?area=1&fechaDesde=2026-01-01&fechaHasta=2026-07-31` → primera llamada debe responder `202 Accepted` con `estado: "generando"`.
2. Poll cada 15s hasta `estado: "listo"` (timeout razonable: varios minutos, dado que implica scraping real).
3. Confirmar que `licitaciones[].tivitParticipo` incluye al menos una licitación en `false` (la prueba real de que esto ya no es solo "encuentros directos").
4. Repetir la misma llamada (misma clave) → debe responder inmediato desde cache, sin volver a scrapear.

## US5 — Flujo colaborativo go/no-go

1. `POST /api/v1/licitaciones/{id}/interes` → confirma `workspaceId: null`, `conversacionId: null`.
2. Repetir el mismo POST → confirma que devuelve el mismo `id` (idempotente, no crea una segunda fila).
3. Disparar el análisis y crear la conversación vía los endpoints existentes de Analisis/Mensajería, luego `PATCH /interes/vincular` con ambos IDs.
4. `GET /api/v1/licitaciones/{id}/interes` → confirma que ahora expone `workspaceId`/`conversacionId` completos.
5. Agregar 2 usuarios como participantes de la conversación, cada uno postea un comentario vía `POST /mensajes` → confirma que ambos ven los comentarios del otro (mismo `conversacionId`).
6. Cambiar manualmente el `codigo_estado` de la licitación en base de datos y volver a pedir `GET /interes` → confirma que el frontend puede detectar la discrepancia contra `estadoLicitacionAlMarcar` (FR-017).
