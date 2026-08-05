---
description: "Task list for Inteligencia de competencia, alertas interactivas y canal de correo (024-inteligencia-competencia-alertas)"
---

# Tasks: Inteligencia de competencia, alertas interactivas y canal de correo

**Input**: Design documents from `specs/024-inteligencia-competencia-alertas/`
**Prerequisites**: plan.md, spec.md, research.md, data-model.md, quickstart.md

## Phase 1: Setup

- [X] T001 Confirmar que `V097` sigue siendo el siguiente número de migración libre en `src/MPM.Api/Database/Scripts/` (la migración más alta hoy es V096).
- [X] T002 Spike de muestreo (research.md R3): tomar 20-30 `codigo_externo` de licitaciones adjudicadas de distintos tipos/organismos y confirmar manualmente qué % tiene "Cuadro de Ofertas" disponible en su ficha pública. Documentar el resultado en `research.md` R3 antes de comprometerse al alcance completo de recolección sobre las 126k.
- [X] T003 [P] Scaffold del módulo nuevo `MPM.Modules.Competidores` (estructura estándar `Controllers/`, `Services/`, `Data/`, `Models/`, `ModuleRegistration.cs`, más su proyecto de test `tests/MPM.Modules.Competidores.Tests`) — usar el mismo patrón que módulos existentes (ver `MPM.Modules.Alertas` como referencia), registrado en `MPM.sln` y `Program.cs` vía `AddCompetidoresModule()`.

## Phase 2: Foundational

- [X] T004 Crear `src/MPM.Api/Database/Scripts/V097__Create_Licitaciones_Ofertas.sql` con la tabla `licitaciones_ofertas` (ver `data-model.md`), incluyendo `UNIQUE (licitacion_id, rut_proveedor)` e índice `pg_trgm` sobre `nombre_proveedor`.
- [X] T005 Crear `src/MPM.Api/Database/Scripts/V098__Create_Competidores_Analisis.sql` con la tabla `competidores_analisis` (ver `data-model.md`), incluyendo `UNIQUE (nombre_competidor, fecha_desde, fecha_hasta)`.
- [X] T006 Exponer el cliente de Vertex AI/Gemini ya usado por `MPM.Modules.Analisis` como servicio inyectable desde `MPM.Shared` (research.md R2), sin duplicar configuración, para que `MPM.Modules.Competidores` lo consuma.

*Nota: T004-T006 son prerequisito de US1 exclusivamente. US2 y US3 no dependen de la Fase 2 y pueden arrancar en paralelo.*

---

## Phase 3: User Story 1 — Panel de inteligencia de competencia (P1)

**Goal**: Buscar un competidor, ver todas sus ofertas recolectadas, y pedir un análisis de IA bajo demanda por rango de fechas, cacheado.

**Independent Test**: Con datos de ofertas ya recolectados, buscar un competidor, ver su listado, pedir el análisis para un rango, confirmar que se genera y que una segunda consulta idéntica reutiliza el resultado guardado.

- [X] T007 [US1] Nuevo módulo scraper `tools/scraper-mp/modulos/cuadroOfertas.js`: dado un `codigo_externo` de licitación adjudicada, navega a su ficha pública, hace clic en "Cuadro de ofertas", extrae RUT/nombre/monto/estado de cada oferente (research.md R3).
- [X] T008 [US1] Integrar `cuadroOfertas.js` al ciclo del scraper existente (`agente-mp.js`): para licitaciones en estado Adjudicada/Cerrada sin ofertas recolectadas todavía, correr el nuevo módulo y persistir vía la API interna (nuevo endpoint o reuso del patrón ya usado para guardar datos scrapeados).
- [X] T009 [P] [US1] `src/MPM.Modules.Competidores/Data/OfertasHandler.cs` + stored procedures `usp_LicitacionesOfertas_Guardar`, `usp_LicitacionesOfertas_BuscarPorCompetidor` (búsqueda `ILIKE`/`pg_trgm` sobre `nombre_proveedor`, research.md R4).
- [X] T010 [P] [US1] `src/MPM.Modules.Competidores/Data/CompetidorAnalisisHandler.cs` + stored procedures `usp_CompetidoresAnalisis_Buscar` (por nombre+rango exacto, research.md R5) y `usp_CompetidoresAnalisis_Guardar` (`INSERT ... ON CONFLICT DO NOTHING`, para resolver la concurrencia del Edge Case de dos usuarios pidiendo el mismo análisis a la vez).
- [X] T011 [US1] `src/MPM.Modules.Competidores/Services/CompetidorAnalysisService.cs`: orquesta — busca análisis cacheado primero (FR-005); si no existe, cuenta cuántas licitaciones entrarían (FR-006) y expone ese conteo antes de confirmar; al confirmar, arma el prompt para Gemini con las ofertas del competidor en el rango, guarda el resultado.
- [X] T012 [US1] `src/MPM.Modules.Competidores/Controllers/CompetidoresController.cs`: `GET /api/v1/competidores?nombre=` (listar ofertas), `POST /api/v1/competidores/analisis` (con conteo previo si no se confirma, análisis real si se confirma).
- [X] T013 [P] [US1] `src/mpm-web/src/pages/CompetidoresPage.tsx` + hook `useCompetidores`: buscar por nombre, tabla de ofertas, selector de rango de fechas, botón "Analizar con IA" que primero muestra el conteo (FR-006) y luego confirma.
- [X] T014 [P] [US1] Unit tests en `tests/MPM.Modules.Competidores.Tests/`: `CompetidorAnalysisServiceTests` (verifica que reutiliza caché ante consulta idéntica, que nunca dispara Gemini sin confirmación explícita — FR-004), `OfertasHandlerTests`/source-guards de los stored procedures nuevos.

**Checkpoint**: US1 completo — panel de competidor funcional en local, con caché verificado.

---

## Phase 4: User Story 2 — Alerta interactiva por Telegram (P2)

**Goal**: Botón "Me interesa" en la alerta de Telegram que responde con un resumen rápido sin IA.

**Independent Test**: Disparar una alerta de prueba, confirmar que trae el botón, presionarlo, confirmar que llega el resumen en <10s sin ninguna llamada a Gemini.

- [X] T015 [P] [US2] `src/MPM.Modules.Alertas/Services/TelegramNotificationService.cs`: agregar `reply_markup.inline_keyboard` con botón `{"text":"Me interesa","callback_data":"interesa:<licitacionId>"}` al `sendMessage` que dispara la alerta.
- [X] T016 [US2] `src/MPM.Modules.Alertas/Controllers/TelegramWebhookController.cs`: detectar `callback_query` en el payload del webhook (además de `message`/`/start`), parsear `interesa:<licitacionId>` de `callback_query.data`.
- [X] T017 [US2] Nuevo método en `MPM.Modules.Alertas` (o servicio compartido) que, dado un `licitacionId`, llama `ApiMpService.GetDetalleAsync` (ya existe en `MPM.Modules.Licitaciones`, sin cambios) y arma un resumen legible (descripción, organismo, monto, fechas, requisitos principales) — sin invocar Gemini (FR-008).
- [X] T018 [US2] Responder el resumen armado vía `sendMessage` al mismo `chat.id` del `callback_query`.
- [X] T019 [P] [US2] Manejar el caso de doble click sobre el mismo botón (Edge Case del spec): responder de forma consistente (reenviar el mismo resumen) sin duplicar trabajo ni fallar.
- [X] T020 [P] [US2] Tests en `tests/MPM.Modules.Alertas.Tests/Controllers/TelegramWebhookControllerTests.cs`: verificar que un `callback_query` con `interesa:<id>` válido dispara el flujo de resumen, que el fail-closed del webhook (BUG-009, secret token) se sigue respetando también para `callback_query`, y que ninguna llamada a Gemini ocurre en este camino (source-guard o mock que falle el test si se invoca el cliente de IA).

**Checkpoint**: US2 completo — botón funcional en local, resumen sin IA verificado.

---

## Phase 5: User Story 3 — Canal de alertas por correo (P3)

**Goal**: Configurar un correo de alertas y recibir las notificaciones también (o alternativamente) por ese canal.

**Independent Test**: Configurar un correo, disparar una alerta de prueba, confirmar que llega el correo con el mismo contenido informativo que Telegram.

- [X] T021 [US3] Crear `src/MPM.Api/Database/Scripts/V099__Add_Email_Alertas_Destinatarios.sql`: `ALTER TABLE alertas_destinatarios ADD COLUMN email_alertas VARCHAR(200);` y extender `usp_AlertasDestinatarios_GuardarChatId` (o crear `usp_AlertasDestinatarios_GuardarEmail` análogo) para persistir el correo.
- [X] T022 [P] [US3] `src/MPM.Modules.Alertas/Services/EmailNotificationService.cs`: arma el HTML del correo (mismo contenido que `TelegramNotificationService.FormatearMensaje`, adaptado a HTML) y llama `IEmailService.SendEmailAsync` (ya existente, inyectado desde `MPM.Shared`).
- [X] T023 [US3] Endpoint nuevo `POST /api/v1/alertas/mi-email` (análogo a `POST /api/v1/alertas/mi-telegram`) en `AlertasController` para que el usuario configure su correo.
- [X] T024 [US3] Extender `AlertasMatchingService.ProcesarGrupoAsync` para, además del envío a Telegram existente, intentar el envío por correo a los destinatarios con `email_alertas` configurado — cada canal se intenta de forma independiente, el fallo de uno no bloquea al otro (FR-011).
- [X] T025 [P] [US3] Frontend: agregar el campo de correo en el modal "Mi Telegram" de `AlertasPage.tsx` (renombrar o agregar sección "Mis canales de alerta") + hook `useGuardarMiEmail`.
- [X] T026 [P] [US3] Tests en `tests/MPM.Modules.Alertas.Tests/`: verificar que el fallo simulado de un canal (ej. SMTP) no impide el intento en el otro canal (Telegram), y que `EmailNotificationService` arma el HTML correctamente escapado (mismo cuidado que `EscaparMarkdownV2` mostró ser necesario para Telegram — revisar si HTML necesita un escape análogo).

**Checkpoint**: US3 completo — canal de correo funcional en local, entrega independiente verificada.

---

## Phase 6: Polish & Deploy

- [X] T027 `dotnet build MPM.sln` y `dotnet test` completo — confirmar cero regresiones en todos los módulos, incluyendo el nuevo `MPM.Modules.Competidores`.
- [X] T028 Ejecutar `quickstart.md` completo contra el ambiente local (`docker compose`) para las tres historias.
> ⚠️ **Reconciliado 2026-08-03**: T029 confirmado hecho (V097-V099 aplicadas, V116 ya corrida encima). T030 confirmado NO hecho — `/competidores` está deliberadamente redirigida a `/licitaciones` en `App.tsx` desde el commit `ed76e20` (2026-07-10, "ocultar hasta tener dataset real"). En su momento, T031 no había podido completarse por una causa raíz real: `scraper-job` fallaba el 100% de sus corridas en producción desde 2026-07-20 (faltaba `ssl` en el `Pool` de `tools/scraper-mp-v2/modulos/db.js` — Cloud SQL exige conexión encriptada) — el ciclo abortaba en <1s antes de llegar a `cuadroOfertas.js`, así que `licitaciones_ofertas` seguía vacía.
>
> **Actualización 2026-08-04**: el usuario autorizó investigar y arreglar ese bug (commits `22ade16`, `49204e0`, `1b029e8`) — SSL vía flag explícito `DB_SSL=true`, memoria/CPU aumentada del job, y secretos `MP_RUT`/`MP_PASSWORD` correctamente pasados al scraper (nunca se habían pasado en ningún deploy). **Verificado en vivo contra logs reales de producción** (no solo `gcloud run jobs executions list`, que puede reportar "completado" aunque el ciclo aborte en <1s): la ejecución `scraper-job-djkmt` del 2026-08-03T22:06 corrió un ciclo completo de 98s, con navegador y conexión a DB abiertos/cerrados sin error SSL. El bug de T031 está **resuelto**, pero T031 en sí (correr el scraper de competidores contra una muestra de licitaciones adjudicadas para poblar `licitaciones_ofertas`) todavía no se ha ejecutado — solo se confirmó que ya no está bloqueado por el bug de SSL. Ver memoria `project_scraper_job_ssl_roto_prod`.

- [x] T029 Desplegar migraciones V097-V099 y el backend actualizado a producción (`tivit-cu010`), siguiendo el mismo mecanismo ya usado en `023-fix-bugs-produccion` (build local + push a Artifact Registry + `gcloud run deploy mpm-api`).
- [X] T030 Reactivar `/competidores` en el frontend (`App.tsx` + link de menú en `AppLayout.tsx`) — **hecho 2026-08-05, en local/Docker**. Se destrabó porque el motivo original ("dataset sin volumen suficiente") era consecuencia directa de un bug real en `cuadroOfertas.js` (ver T031): la tabla del Cuadro de Ofertas elegida por `buscarTablaEnDocumento` era la contenedora de layout (`tablaInformacion`), no la grilla de datos anidada (`grdSupplie...`), porque el filtro comparaba contra `innerText` de toda la tabla en vez de solo su fila de encabezado — reportaba "0 ofertas" en licitaciones con datos reales (confirmado en vivo con 622-14-LP25: 4 oferentes reales, incluyendo TIVIT). Corregido, validado en Docker con `/competidores` mostrando datos reales (CLARO, ENTEL, GTD, IT4U, etc.) — **pendiente el deploy a producción real** (build + push + `gcloud run deploy` de `mpm-api` y `mpm-web`).
- [X] T031 Ejecutar el scraper localmente sobre una muestra de licitaciones adjudicadas para poblar `licitaciones_ofertas` — **hecho 2026-08-05**. Además del fix de `cuadroOfertas.js` de arriba, se corrigió un leak real en `competidor-mercado.js` (`cerrarFicha` pasaba `isPopup=false` fijo en vez del valor real, nunca cerraba el popup de la ficha) y se agregó concurrencia acotada (3 tabs simultáneas, configurable vía `MP_COMPETIDOR_CONCURRENCIA`) — validada con 5 solicitudes HTTP reales simultáneas sin caídas ni degradación del contenedor. `licitaciones_ofertas` tiene datos reales en local; falta correr el backfill contra producción.
- [ ] T032 Re-ejecutar `quickstart.md` contra producción real para las tres historias — depende de desplegar T030/T031 a producción (todavía no se hizo, solo se validó en local/Docker).
- [ ] T033 Actualizar `CLAUDE.md`: marcar `024-inteligencia-competencia-alertas` con su estado real tras el deploy — de baja prioridad, `CLAUDE.md` ya no hardcodea el número de migración más alta (apunta a revisar `Scripts/` directamente).

## Dependencies

- **Fase 2 (Foundational)** bloquea únicamente a **US1** — US2 y US3 no dependen de ella.
- **US1, US2, US3** son independientes entre sí — se pueden trabajar en paralelo por personas/agentes distintos.
- **Fase 6 (Polish & Deploy)** depende de que las historias que se vayan a desplegar juntas estén completas y validadas en local (no hace falta esperar a las tres si se decide desplegar incrementalmente).

## Parallel Execution Examples

```text
# Las tres historias en paralelo (agentes/desarrolladores distintos), tras Fase 1:
US1 (T007-T014, requiere Fase 2 primero) ‖ US2 (T015-T020) ‖ US3 (T021-T026)

# Dentro de US1, en paralelo entre sí:
T009 (OfertasHandler) ‖ T010 (CompetidorAnalisisHandler) ‖ T013 (frontend)
```

## Implementation Strategy

**MVP sugerido = US2** (P2, pero la más chica y con pedido explícito de negocio ya confirmado) si se necesita el primer incremento entregable más rápido. **US1** es la de mayor valor estratégico pero también la de mayor alcance (scraper nuevo + módulo nuevo + spike de validación de datos primero) — no bloquea a las otras dos, se puede desarrollar en paralelo sin esperarla. **US3** es la más chica en esfuerzo (reusa infraestructura de correo existente) y puede entrar en cualquier momento.
