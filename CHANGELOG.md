# Changelog

Todos los cambios notables de este proyecto se documentarán en este archivo.

El formato está basado en [Keep a Changelog](https://keepachangelog.com/es-ES/1.0.0/),
y este proyecto adhiere a [Semantic Versioning](https://semver.org/lang/es/).

## [Unreleased]

### Fixed

- **035 — Scraper: falsos "postback colgado" con 0 resultados (corridas incrementales fallaban siempre)**
  - **Bug real diagnosticado en vivo** (12/08, stack docker): en modo `--incremental` el rango de fechas es corto (última sync + 5 días) y el portal devuelve *"No se han encontrado resultados para su búsqueda."* — texto que `firmaResultados()` en `modulos/buscar.js` no matcheaba (`/Se encontraron/`), la firma del área de resultados quedaba idéntica y el scraper trataba la búsqueda vacía legítima como página colgada: 45s de timeout × 5 estados × 2 intentos ≈ 8 min por ciclo, terminando con `0 de 5 estados exitosos` y error en `scraper_sync_log`
  - **Fix**: la firma ahora detecta también el texto "No se han encontrado resultados" (firma `sin-resultados`) — el postback se da por completado y el ciclo sigue, reportando 0 licitaciones como resultado legítimo
  - **Verificación**: reproducción exacta del fallo con `MP_FECHA_DESDE=07-08-2026` (fallaba) → fix aplicado (los 5 estados completan en ~30s, 0 licitaciones, sin error); regresión con rango largo (01-01-2025) sigue extrayendo 32 licitaciones en estado Adjudicada
  - Diagnóstico: sesión persistida válida (login OK), selectores/filtros/página OK — el problema era exclusivamente la señal de finalización del postback con 0 resultados

### Added

- **036 — Flujo Comercial de Ofertas (Fases 1, 2 y 3: Pliegos, Census, GO/NO GO, Propuestas DOCX y Google Drive)**
  - **Fase 1 (Descarga de pliegos y análisis on-demand)**: descarga bajo demanda de adjuntos de Mercado Público sin sesión logueada (resuelve reCAPTCHA Enterprise y previene bloqueos de sesión), almacenamiento en GCS/local con hash SHA-256 (V141). Zona IA on-demand para análisis comercial de bases con cache por conjunto de documentos (V142).
  - **Fase 2 (Match de capacidades Census y GO/NO GO formal)**: módulo `MPM.Modules.Censo` para integración con API interna de Census (auth JWT, renovación automática con `SemaphoreSlim` y retry 401). Expansión multi-capa de conceptos (Capa 1/2 catálogo local, Capa 3 IA cacheada). Búsqueda multi-skill paralela (semáforo 8) con cache 24h por tecnología y dedup por email. Decisión GO/NO GO formal en `MPM.Modules.Colaboracion` con snapshot inmutable de recomendación IA y motivo obligatorio en NO GO (migraciones V143, V144). Componentes frontend `CapacidadesTIVITPanel` y `DecisionGoNoGoPanel`.
  - **Fase 3 (Generador DOCX de propuestas y avisos in-app)**: módulo `MPM.Modules.Propuestas` con CRUD de catálogos corporativos (experiencias manuales, certificaciones con PDF sincronizadas desde `user-certifications` de Census, 10 capítulos canónicos) (V145). Motor OpenXML `DocxProposalGenerator` con plantilla oficial `tivit_proposal_template.docx` de PRJ-001. Generación síncrona versionada (`POST /propuestas/generar`, `GET /archivo`, `PATCH .../estado`). Sistema de avisos in-app a personas elegidas a mano vía `MPM.Modules.Notificaciones` (V147). Componente frontend `PropuestaPanel.tsx` integrado en el drawer de detalle.
  - **Fase 3.5 / Bundle D (Exportación a Google Drive)**: servicio `GoogleDriveService` (`IGoogleDriveService`) con soporte para exportación directa a Google Drive corporativo y almacenamiento local simulado para entornos de desarrollo. Endpoint `POST /api/v1/licitaciones/{codigo}/propuestas/{id}/exportar-drive` con botón de exportación en la tabla de historial de propuestas del frontend.
  - **Especificación y Governance**: formalización de Historias de Usuario `HU-PRO-001` a `HU-PRO-007` en `docs/api-first/propuestas.md`.
  - **Tests y Cobertura E2E**: tests unitarios de catálogo, sincronización, generación DOCX, avisos y servicio de propuestas (`MPM.Modules.Propuestas.Tests`), tests de integración HTTP (`tests/MPM.Tests/Integration/PropuestasApiTests.cs`) y suite E2E de Playwright (`src/mpm-web/e2e/specs/propuestas.spec.ts` + `PropuestasPanel.ts`). **Suite E2E completa ejecutada contra Docker: 97/97 tests aprobados (0 fallos) ✓**.

- **034 — Centro de Administración: usuarios, roles y logs (Admin/SuperAdmin)**
  - **Backend (`MPM.Modules.Administracion`, nuevo módulo)**: gestión de usuarios (listado paginado, creación con contraseña temporal bcrypt, activación/desactivación, cambio de rol, flag account manager de gobierno) y lectura unificada de logs/auditoría (auth, sync, scraper, extracción de documentos, proveedor IA) — migraciones V131 (SPs `usp_Admin_*`) y V132 (`usp_Admin_ListarLogs` con UNION ALL normalizado)
  - **Jerarquía de roles** (`AdminRoleRules`): SuperAdmin > Admin > Analista/Usuario; un Admin gestiona solo Analista/Usuario, solo SuperAdmin gestiona Admins/SuperAdmins, y nadie puede desactivarse/cambiarse el rol a sí mismo — validada en servicio + controllers `[Authorize(Roles = "Admin,SuperAdmin")]`
  - **Frontend**: sección "Administración" en el menú (visible para Admin y SuperAdmin) con `/admin/usuarios` (tabla + modales de creación/cambio de rol, pensados para usuarios no técnicos), `/admin/logs` (resumen del sistema + pestañas por origen) y `/admin/config-ia` (switch de IA movido desde `/admin/ia` con redirect)
  - **Docs**: `docs/api-first/admin.md` (spec del módulo) + specs api-first de Alertas, Competidores, Notificaciones y Colaboración; manual de usuario en `docs/manual-usuario/`; README/QUICKSTART actualizados con el modelo de roles
  - **Tests**: 32 tests xUnit de jerarquía/validación (`MPM.Modules.Administracion.Tests`) + E2E Playwright `admin-usuarios.spec.ts` y `admin-logs.spec.ts` (10/10 contra stack docker)
  - **Fixes encontrados en testing con Docker** (causa raíz + corrección):
    - `TenantMiddleware` leía `FindAll("role")`, pero el claim JWT corto se re-mapea a `ClaimTypes.Role` al deserializar — `TenantContext.Roles` siempre llegaba vacío (bug latente pre-existente; nadie lo usaba). Corregido a `ClaimTypes.Role`; `AdminUsuariosController.Actor()` lee del principal autenticado
    - V133: columnas OUT de `usp_Admin_ListarUsuarios` con prefijo `p_` que Dapper no mapeaba (`p_total_count` → TotalCount)
    - V134: `ORDER BY` inválido en el UNION ALL de `usp_Admin_ListarLogs` (los nombres de columna salen del primer SELECT, sin alias)
    - V135: tipos `text` vs `VARCHAR(20)` en el RETURN QUERY (42804) — casts explícitos en literales y CASE
    - `Dockerfile` de la API: faltaba `COPY` del csproj del nuevo módulo antes del restore
    - Frontend: Select de rol con opciones custom altas rompía el listbox virtualizado de AntD — opciones en texto plano + `virtual={false}` + descripción del rol como `extra` del Form.Item; fix de `Switch size="large"` inválido que bloqueaba `tsc`
    - **E2E desactualizados vs contrato vigente (V108)**: `catalogo.spec.ts` esperaba ≥8 estados y slugs PascalCase (`Licitacion`, `TratoDirecto`...) del modelo anterior a V108 — actualizados al contrato real (5 estados vigentes `5,6,7,8,15`, slugs minúscula `lp/td/co/ca`); `ResetPasswordPage.ts` buscaba `.ant-result-subtitle` de la UI pre-rediseño 019 — el aviso de token inválido ahora se lee por su texto real ("expirado o ya fue utilizado"). Resultado: **suite E2E completa 91/91 ✓** (antes 87/91)
  - Fix menor: `Switch size="large"` inválido en `AdminConfiguracionIaPage.tsx` (bloqueaba `tsc`)

- **033 — Migración del proveedor de IA (Gemini → Qwen 3.7 G4) con switch de super admin**
  - **Abstracción del proveedor (US1)**: contrato `ILlmClient` en `MPM.Shared` (request neutral `LlmRequest` con texto/PDF/GCS, JSON mode, presupuesto de tokens) + resolución dinámica por request vía `LlmClientResolver` (MPM.Core) con precedencia **BD > entorno > default** y cache de 30s; `VertexGeminiClient` pasa a implementar el contrato (sin cambios de comportamiento con Gemini); los 4 usos de IA (análisis de PDFs, chat, búsqueda semántica, sinónimos de alertas) migran del cliente directo/HTTP crudo al resolver — `modelo_usado` persiste el modelo real que ejecutó cada análisis
  - **Cliente OpenAI-compatible (US3)**: `OpenAiCompatClient` para servir Qwen 3.7 G4 (vLLM/Ollama/llama.cpp) vía `/v1/chat/completions` con PDFs como data URI y `response_format: json_object`; excepción tipada transversal `LlmRespuestaBloqueadaException` (Gemini hereda de ella); registrado por key `openai`
  - **Benchmark de calidad (US2)**: harness `tools/BenchmarkLlm` que compara Gemini vs. Qwen con el MISMO prompt de producción, paridad de campos críticos (montos, fechas, puntuaciones), latencia p50/p95, JSON válido y veredicto go/no-go contra el umbral ≥ 90%
  - **Switch del super admin (US4)**: tabla `system_ai_provider` (migración V130) + SPs `usp_SystemConfig_Obtener/ActualizarAiProvider` (UPSERT auditado, historial por `record_status`); endpoint `GET/PUT /api/system/ai-provider` con rol `SuperAdmin` (403 para otros roles); página `/admin/ia` con switch gcloud/qwen (efecto en el análisis siguiente, sin reinicio, persistente entre reinicios, auditoría usuario/fecha); item de menú solo visible para SuperAdmin

- **032 — Mejora de alertas por correo**
  - **US1 (matching sin falsos positivos)**: `AlertasMatchingService.EvaluarMatch` reemplaza `string.Contains` por comparación con límites de palabra (`Regex.IsMatch` con `\b`) para keyword y sinónimos IA — corrige el caso reportado por el usuario ("TI" matcheaba "parTIcipantes"), sin romper el matching de frases multi-palabra existentes
  - **US2 (correo enriquecido)**: el correo de alerta ahora incluye organismo, fecha de cierre y enlace directo a la ficha en Mercado Público cuando están disponibles (V129, amplía `usp_Licitaciones_ListarParaMatching` con `fecha_cierre`/`link`, columnas ya existentes) — cada campo se omite prolijamente si falta el dato
  - **US3 (horario)**: Cloud Scheduler `sync-job-scheduler` pasa de `0 3,15 * * *` a `0 8,15 * * *` (hora de Santiago) — cambio de infraestructura, sin código

- **019 — Rediseño frontend por pantalla**
  - Dos componentes compartidos nuevos en `src/mpm-web/src/components/`: `StatusBadge.tsx` (6 variantes semánticas — `neutral/info/warning/success/error/tertiary` — sobre los tokens del theme de `main.tsx`) y `PageHeader.tsx` (chip de ícono siempre en `colorPrimary`), reemplazando 5 implementaciones de badge de estado y 3 estructuras de header divergentes encontradas en una auditoría de consistencia
  - **Licitaciones**: corregida la grilla de tarjetas de estadísticas por estado (dejaba un hueco de alineación cuando el número de estados no completaba la fila, ej. junto a "Revocada"); densidad visual reducida — las tarjetas de resumen pasan de `Card`+`Statistic` a una fila compacta
  - **Análisis** (lista, workspace, dashboard, chat): rediseño completo del módulo de mayor prioridad — `StatusBadge`/`PageHeader` en las 4 pantallas, tarjetas de workspace migradas a `Card` de Ant Design, botones con gradiente inline reemplazados por el estilo `primary` del theme, emojis reemplazados por `@ant-design/icons`
  - **Catálogos, Mensajería, Ejecutivo, Alertas, Competidores**: mismos componentes compartidos adoptados; `MensajeriaPage.tsx` reconstruida sobre `Layout`/`Layout.Sider` de Ant Design en vez de `div`+estilos inline, sin tocar los hooks de tiempo real (`useChatLogic`, `usePresencia`)
  - Notificaciones queda explícitamente fuera de alcance
  - Diferido: comparativa nueva de "cobertura de mercado" en Ejecutivo (requiere endpoint backend nuevo, `contracts/ejecutivo-cobertura-mercado.md`) — no implementada en esta pasada
  - Trabajo hecho en una rama dedicada (no en `src/mpm-web` de `dev` directamente) tras descartar un intento previo de otro agente que había anidado un proyecto duplicado dentro del mismo `src/mpm-web`

- **031 — Feedback ChileCompra (filtro por área, estadísticas de estado, orden de análisis, competidores ampliado, flujo colaborativo go/no-go)**
  - **Filtro por área de negocio (US1)**: catálogo `areas_negocio` (migración V118, semilla Cloud/Ciberseguridad/Digital) + función `fn_licitacion_area_codigos` que clasifica licitaciones en consulta contra el `search_vector` (tsvector) ya existente, sin columna nueva en `licitaciones`; parámetros `area`/`sinClasificar` en `usp_Licitaciones_Listar` (V119, reparado en V125 — ver Fixed) y `GET /api/v1/licitaciones`; endpoint `GET /api/v1/catalogos/areas-negocio`; selector de área + toggle "sin clasificar" en `LicitacionFilterBar.tsx`
  - **Estadísticas de estado con drill-down (US2)**: nuevo SP `usp_Licitaciones_ContarPorEstado` (V120) — los 5 estados reales (Publicada/Cerrada/Desierta/Adjudicada/Revocada) siempre aparecen, incluso con conteo 0; endpoint `GET /api/v1/licitaciones/estadisticas-estado`; tarjetas clicables en `LicitacionesPage.tsx` que navegan al listado filtrado por ese estado
  - **Orden de análisis por fecha de adjudicación (US3)**: `usp_AnalisisWorkspaces_Listar` (V121) reescrito para ordenar por `COALESCE(fecha_adjudicacion, fecha_estimada_adjudicacion) DESC NULLS LAST` en vez de la fecha de creación del análisis; campo `fechaAdjudicacion` expuesto en el DTO y mostrado en `AnalisisListPage.tsx`
  - **Flujo colaborativo go/no-go (US5)**: módulo nuevo `MPM.Modules.Colaboracion` — tabla `licitaciones_interes` (V122, `UNIQUE(licitacion_id)` garantiza idempotencia) vincula una licitación marcada "de interés" a un `analisis_workspace` y una `conversacion` de Mensajería (reutilizadas sin cambio de esquema); `usp_AnalisisWorkspaces_Crear` corregido a idempotente por `licitacion_id` (V123, bug latente preexistente); endpoints `POST/GET /api/v1/licitaciones/{id}/interes`, `PATCH .../interes/vincular`, `GET /api/v1/licitaciones/interes`; componente `LicitacionInteresPanel.tsx` en el drawer de detalle (marcar interés, ver asignados, comentar) — validado E2E con Playwright
  - **Actividad total de mercado de competidores (US4)**: nuevo script standalone `tools/scraper-mp-v2/competidor-mercado.js` + `modulos/buscarPublico.js` — búsqueda pública de licitaciones por HTTP plano (sin Playwright, sin login) contra `POST /BuscarLicitacion/Home/Buscar` de Mercado Público, acotada por área+período, seguida de una visita con Playwright a cada ficha candidata para revisar el Cuadro de Ofertas y detectar participación del competidor incluso en licitaciones donde TIVIT no ofertó; cache `competidores_actividad_mercado` (V124, patrón get-or-generate en background vía `CompetidorMercadoService`); endpoint `GET /api/v1/competidores/{nombre}/actividad-mercado`; panel en `CompetidoresPage.tsx` con polling y distinción visual "encuentro directo" vs. "brecha de mercado"

- **017 — Ajustes Urgentes del Cliente (UI/UX, Sesión y Coherencia del Análisis)**
  - **Sesión (US1)**: cliente HTTP central `src/mpm-web/src/lib/apiClient.ts` — ante cualquier 401 cierra sesión una sola vez, redirige a `/login` y muestra "Tu sesión expiró"; los 18 hooks migrados del `fetch` crudo al wrapper; E2E `session-expired.spec.ts`
  - **Coherencia documental (US2)**: sección `validacion_documental` en el prompt de Gemini + post-proceso determinístico `ValidacionDocumentalService.cs` que cruza los archivos realmente subidos al workspace contra lo que el acta declara faltante (detecta el caso "perdimos por no enviar X pero X sí se envió"); componente `ComparativaDocumentos.tsx` en el resumen del dashboard; 6 unit tests
  - **Licitaciones (US3)**: pantalla rediseñada — buscador único, sin "Búsqueda inteligente" ni botón "Sincronizar", botón "Reiniciar filtros", layout compacto; sync automático semanal configurable (`Sync:IntervalDays`/`Sync:WindowDays`) con backfill idempotente desde 01-01-2025 (marca `BACKFILL25`, migración V076)
  - **Análisis (US4)**: chat contextual en vista propia `/analisis/:id/chat` (componente `AnalisisChat.tsx` extraído); normalización de Markdown en respuestas del chat + system prompt endurecido; exportación PDF estructurada con `jspdf-autotable` (texto seleccionable, tablas paginadas, incluye comparativa de documentos) en reemplazo de la captura html2canvas; prompt de análisis profundizado (evidencia citada, brechas cuantificadas, recomendaciones priorizadas)
  - **UI general (US5)**: login sin enlace "¿Olvidaste tu contraseña?"; sidebar con avatar + rol ("admin TIVIT") sin correo; borrar notificaciones individual y masivo (migración V075, endpoints `DELETE /api/v1/notificaciones[/{id}]`, nuevo proyecto `MPM.Modules.Notificaciones.Tests` + tests de integración); explicaciones de conceptos en Catálogos al hacer click (Drawer con definiciones ChileCompra); dashboard ejecutivo con jerarquía visual mejorada
  - **Investigación (US6)**: `docs/investigacion-victorias-licitaciones.md` — factibilidad de explicar por qué se ganan licitaciones (solo documento, sin código)
  - **Eliminado en este lote**: componentes `LicitacionSearchBar.tsx` y `SyncButton.tsx` (buscador duplicado y sincronización manual — la sincronización pasa a ser exclusivamente automática; el endpoint `POST /sync` se conserva para uso operacional)

- **Fase 4 — Notificaciones y Seguimiento Activo (US3)**
  - Nuevas tablas `licitaciones_seguidas` (opt-in por usuario) y `licitaciones_aclaraciones` (idempotencia con `UNIQUE(codigo_externo, codigo_aclaracion)`)
  - 6 stored procedures: `usp_Licitaciones_SeguirToggle`, `usp_Licitaciones_EsSeguida`, `usp_Licitaciones_ObtenerParaMonitor`, `usp_Licitaciones_Aclaracion_Upsert`, `usp_Licitaciones_Aclaracion_MarcarNotificada`, `usp_Licitaciones_ObtenerSeguidas`
  - `AclaracionMonitorService.cs`: BackgroundService que cada 30 min consulta la API de Mercado Público para licitaciones activas seguidas (estados 1, 2, 4), detecta nuevas preguntas/aclaraciones y genera notificaciones a todos los seguidores
  - 3 nuevos endpoints REST en `/api/v1/licitaciones`: `POST /{codigo}/seguir` (toggle), `GET /{codigo}/seguida`, `GET /seguidas`
  - Modelo `ApiMpPreguntas` + `ApiMpAclaracion` para capturar el campo `Preguntas.Listado[]` de la API MP
  - Variables de entorno `MONITOR_ENABLED` (default `true`) y `MONITOR_INTERVAL_MINUTES` (default `30`)
  - Frontend — botón estrella (⭐) por fila en la tabla de licitaciones con toggle optimista y feedback `message.success`
  - Frontend — `useEsSeguida`, `useSeguirToggle`, `useLicitacionesSeguidas` hooks en `useLicitaciones.ts`
  - Frontend — `NotificacionesPage.tsx` actualizada: tipo `aclaracion_detectada` con icono estrella dorado y link directo a la licitación; tipo `scraper_config_error` con tag de advertencia

- **Fase 3 — Dashboard Comparativo Ejecutivo (US2)**
  - Nuevo endpoint `GET /api/v1/analisis/ejecutivo?anio=` que agrega todos los análisis completados
  - Migración V071: `usp_Analisis_ObtenerResultadosCompletos` — retorna JSON completos de workspaces completados con filtro de año
  - Nuevos DTOs: `DashboardEjecutivoDto`, `CompetidorRankingDto`, `LicitacionResumenEjecutivoDto`, `ResultadoCompletoDto`
  - Nueva página `EjecutivoDashboardPage.tsx` en ruta `/analisis/ejecutivo` con:
    - KPIs globales: licitaciones analizadas, ganadas/perdidas, montos totales, win rate
    - Puntaje promedio TIVIT vs. ganador con barras de progreso
    - Top 5 factores de pérdida frecuentes (extraídos de los análisis Gemini)
    - Ranking de 17 competidores únicos con frecuencia, ganadas y monto adjudicado — expandible con drilldown por licitación
    - Tabla completa de licitaciones con resultado, adjudicatario, monto y progreso de puntaje
    - Filtro por año

- **Fase 2 — Automatización del Pipeline de Scraping y Análisis IA**
  - Scraper de Mercado Público integrado en el contenedor `api` (Node.js 20 + Playwright Chromium)
  - `ScraperBackgroundService.cs`: ejecución periódica configurable (`SCRAPER_INTERVAL_HOURS`), inyección de variables de entorno al proceso Node, notificaciones de resultado/error vía `NotificacionesService`
  - `tools/scraper-mp/`: módulos `buscar.js` (filtro por estado Adjudicada), `adjuntos.js` (detección de Acta de Evaluación con fallback por nombre), `api-client.js` (JWT service token, upload binario con FormData+Blob, pipeline completo workspace→documento→análisis)
  - `GeminiService.cs`: File API como vía primaria para PDFs escaneados, `inline_data` como fallback; `max_output_tokens` aumentado a 32768; strip de markdown code fences en `ParseGeminiResponse`
  - `AnalisisBackgroundService.cs`: strip de null bytes, validación JSON pre-insert con logging diagnóstico
  - Notificación `scraper_config_error` cuando Node.js o el script no se encuentran al arranque
  - Variables de entorno nuevas: `MP_RUT`, `MP_PASSWORD`, `SCRAPER_ENABLED`, `SCRAPER_INTERVAL_HOURS`, `MP_ANALISIS_IA`, `MP_FECHA_DESDE`, `API_BASE_URL`, `Scraper__ScriptPath`

- **Módulo de Autenticación con recuperación de contraseña**
  - Tabla `usuarios` con contraseñas hasheadas (bcrypt) y soporte multi-tenant
  - Tabla `password_reset_tokens` con expiración y tracking de uso
  - 7 migraciones SQL (V038-V044) para autenticación y recuperación
  - Endpoint `POST /api/v1/auth/forgot-password` para solicitar token de recuperación
  - Endpoint `GET /api/v1/auth/validate-reset-token/{token}` para validar token
  - Endpoint `POST /api/v1/auth/reset-password` para restablecer contraseña
  - Servicio `IEmailService` con implementación SMTP y template HTML
  - Login actualizado para usar tabla de usuarios con verificación bcrypt
  - Seed de 2 usuarios demo (admin@tivit.cl, analista@tivit.cl) con contraseña `test123`

- **Páginas de recuperación de contraseña (Frontend)**
  - `ForgotPasswordPage.tsx`: formulario de solicitud con validación
  - `ResetPasswordPage.tsx`: validación de token + formulario con confirmación de contraseña
  - Rutas `/forgot-password` y `/reset-password/:token` agregadas
  - Persistencia de "Recordarme" en localStorage
  - 8 tests E2E nuevos para flujos de recuperación

- **Mejoras de UI/UX en Login**
  - Corrección ortográfica: "Contrasena" → "Contraseña"
  - Checkbox "Recordarme" con persistencia de email
  - Link "¿Olvidaste tu contraseña?" integrado
  - Header con color neutro oscuro (#1a1a2e) en lugar de rojo
  - Validación inline por campo con mensajes específicos
  - Estados de botón mejorados (loading, disabled)
  - Contraste de bordes de inputs mejorado para accesibilidad

### Changed

- **AuthController.cs**: Login ahora usa `usp_Auth_ValidarUsuario` contra tabla de usuarios real
- **useAuth.ts**: Agregado estado `rememberedEmail` y método `saveRememberedEmail`
- **LoginPage.tsx**: Refactorizado para usar validación inline y persistencia de email
- **Program.cs**: Registrado `IEmailService` como servicio scoped
- **appsettings.json**: Agregada sección `Smtp` para configuración de email

### Fixed

- **031**: `CREATE OR REPLACE FUNCTION` con parámetros nuevos crea un overload separado en vez de reemplazar la función existente cuando cambia la firma de entrada — V119 (`usp_Licitaciones_Listar`) dejó dos versiones coexistiendo (10 y 12 parámetros), causando `function ... is not unique` al llamarla con la firma vieja; detectado por el test suite completo (`tests/MPM.Modules.Licitaciones.Tests`, `tests/MPM.Tests`) y corregido en V125 con `DROP FUNCTION` explícito de la firma vieja, siguiendo el mismo patrón que V079 ya usaba correctamente
- **031**: `LicitacionHandler.ObtenerPorCodigoAsync` nunca mapeaba el campo `id` (bigint) del JSON de licitación al DTO — siempre devolvía `0`; bug preexistente sin detectar hasta que `LicitacionInteresPanel` (US5) empezó a depender del id numérico mostrado en el drawer de detalle; encontrado en vivo vía el test E2E de Playwright de la nueva historia
- `usp_Licitaciones_Listar` no tenía desempate determinístico en el `ORDER BY` — confirmado en vivo que hay lotes de sync con miles de filas compartiendo exactamente el mismo `fecha_publicacion` (mismo segundo), lo que podía devolver un orden distinto entre dos llamadas idénticas y romper la paginación (una fila podía repetirse en dos páginas o desaparecer). Se agrega `l.id DESC` como desempate final en cada rama de ordenamiento (V126)
- `MensajeriaStoredProcedures.ObtenerPresencia` — mismo bug BUG-014 (falta de cast `::jsonb` explícito) que ya se había corregido en `CrearConversacion` pero nunca se aplicó acá; `GET /api/v1/presencia?userIds=...` devolvía 500 (`42883: function usp_presencia_obtener(text) does not exist`) en vez de 200
- `LicitacionApiTests.Catalogos_ReturnsEstados` esperaba ≥8 estados en el catálogo — desactualizado desde la limpieza real de estados en V086 (solo 5 son válidos: Publicada/Cerrada/Desierta/Adjudicada/Revocada); ajustado a la cantidad real

### Security

- Contraseñas hasheadas con bcrypt (cost factor 11) usando pgcrypto
- Tokens de recuperación con UUID sin guiones, expiración de 1 hora
- Invalidación automática de tokens anteriores al solicitar nuevo
- Verificación de existencia de usuario antes de enviar email (sin revelar si existe)
- Template de email con diseño responsive y branding MPM

---

## [Previous]

### Added

- Módulo de Mensajería: chat 1-a-1 y grupal con tiempo real (SignalR)
- 16 endpoints REST para conversaciones, mensajes, adjuntos y presencia
- SignalR Hub con eventos de mensaje, edición, eliminación, typing y presencia
- Soporte de archivos adjuntos con límite configurable (10MB)
- Indicador de escritura (typing) y última conexión (presence)
- Edición de mensajes con ventana de 15 minutos
- Vinculación opcional de conversaciones a licitaciones
- 22 migraciones SQL (V013-V034) para tablas y stored procedures
- Página de mensajería con sidebar de conversaciones y panel de chat
- Tests de integración y E2E para flujos de mensajería
- Runbook operativo con SLOs y plan de incidentes
- API specification document (docs/api-first/mensajeria.md)
- Health check endpoint para módulo de mensajería (/health/mensajeria)
