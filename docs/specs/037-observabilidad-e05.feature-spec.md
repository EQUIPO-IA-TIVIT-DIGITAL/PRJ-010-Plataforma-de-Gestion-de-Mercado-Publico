# Feature Spec: Observabilidad Integral + Costos LLM (E05)

**Feature:** 037-observabilidad-e05
**Rama:** `037-observabilidad-e05`
**Generado por:** orchestrator TIVIT Foundry - skill `observabilidad` + `costos-llm` + `opentelemetry` + `prometheus-grafana` + `langfuse`
**Fecha:** 2026-08-24
**Origen:** Auditoría E05 - diagnóstico reprogramado al final como prioridad. Stack actual sin trazas, métricas ni tracking LLM.
**Superficie REST:** Extiende health checks existentes. No crea recursos de negocio nuevos, por eso usa `feature-spec`.
**Estado:** Spec para validación - no bloquea limpieza actual (pre-nueva subida), se implementa al final como prioridad.
**Prioridad:** Alta al cierre del ciclo de limpieza (después de E04 WIF).

---

## 1. Scope

### Included
- **Trazas distribuidas** con OpenTelemetry .NET SDK: `ActivitySource` MPM, propagación `W3C TraceParent` entre API → DB (Npgsql) → Redis → SignalR Hub → LLM clients. Export OTLP hacia Cloud Trace (GCP `tivit-cu010`).
- **Métricas** con `prometheus-net` + endpoint `/metrics` (solo interno, no público). Métricas: `mpm_http_requests_total`, `mpm_http_duration_seconds`, `mpm_llm_calls_total{provider,modelo}`, `mpm_llm_tokens_total`, `mpm_llm_latency_seconds`, `mpm_sync_licitaciones_total`, `mpm_aclaraciones_detectadas_total`, `mpm_scraper_runs_total{estado}`.
- **Logs estructurados** con Serilog JSON: `correlationId` (`X-Correlation-Id` / `TraceId`) propagado via `TenantMiddleware`, nivel `Information` default, `Warning` para Microsoft.
- **Health checks** estandarizados por módulo: `GET /health/licitaciones`, `/health/analisis`, `/health/censo`, `/health/propuestas`, `/health` agregado. Contratos `{status, module, timestamp, checks}`.
- **Observabilidad LLM** con Langfuse (o Cloud Trace + tabla `llm_usage` si Langfuse se descarta): cada llamada Gemini/Qwen registra `provider`, `modelo_usado`, `prompt_tokens`, `completion_tokens`, `latency_ms`, `costo_estimado_clp`, `traceId`.
- **Dashboard Grafana** (Grafana Cloud o self-host en GCE) con 3 vistas: Operativo (RPS, P95, error rate), Negocio (licitaciones ingestadas/día, aclaraciones/día, scraper success), LLM (calls/día, costo acumulado, latencia por proveedor).
- **Costos LLM** (`costos-llm` skill): tabla `llm_usage` + vista `v_llm_costos_diarios`, API interna `GET /api/v1/admin/llm-costos?desde&hasta` para SuperAdmin.

### Excluded
- Infra Terraform para Grafana/Prometheus (reusa lo de E04 cuando se haga WIF).
- Alerting avanzado (PagerDuty) - solo dashboards lectura en v1.
- Tracing de frontend (solo backend en v1).
- Reemplazo de `AnalisisBackgroundService` Task.Run por Pub/Sub - se documenta como riesgo aceptado, no se reescribe en esta feature.
- Migración de logs históricos.

## 2. Actors & Triggers

| Actor | Rol |
|-------|-----|
| SRE / DevOps | Lee dashboards Grafana y `/metrics`, recibe trazas en Cloud Trace |
| SuperAdmin | Consulta `/api/v1/admin/llm-costos` para control de gasto IA |
| Sistema (MPM.Api) | Emite trazas/métricas/logs en cada request, sync, scraper, LLM call |
| Cloud Scheduler | Dispara `sync-job` / `scraper-job` - sus ejecuciones también trazadas via `WORKER_MODE` |

**Triggers:** cada request HTTP, cada ciclo `SyncEngineService`, `AclaracionMonitorService`, `ScraperBackgroundService`, cada `LlmClientResolver` call.

## 3. Data Touched

| Entidad | Lectura/Escritura | Detalle |
|---------|-------------------|---------|
| `llm_usage` (nueva) | Escritura | `id, trace_id, provider, modelo, prompt_tokens, completion_tokens, latency_ms, costo_clp, licitacion_id, created_at` |
| `analisis_licitacion_comercial.modelo_usado` | Lectura | Ya existe (Gemini/Qwen) - se cruza con `llm_usage.trace_id` |
| `System.Diagnostics.Activity` | Escritura | No es tabla, es contexto W3C en memoria |
| `prometheus-net` registry | Escritura | Métricas en memoria expuestas en `/metrics` |
| Serilog sink | Escritura | JSON a stdout (luego Cloud Logging) |

**Migración:** `V156__Create_llm_usage.sql` - tabla + `v_llm_costos_diarios` + índices.

## 4. Behavior Spec

- Dado un request `GET /api/v1/licitaciones?montoDesde=50000000`, cuando se procesa, entonces se crea `Activity` raiz `GET /api/v1/licitaciones`, con hijos `usp_Licitaciones_Listar`, `redis:Get` si hay cache, y se propaga `traceparent` en headers de respuesta.
- Dado un análisis comercial que llama a `LlmClientResolver` → `VertexGeminiClient`, cuando completa, entonces se inserta fila en `llm_usage` con tokens reportados por Vertex AI y se añade evento `llm.call` a la traza activa.
- Dado un ciclo `WORKER_MODE=scraper`, cuando termina con 5 estados, entonces se incrementa `mpm_scraper_runs_total{estado="success"}` y se logea JSON con `correlationId` = `scraper-YYYYMMDD-HH`.
- Dado un SuperAdmin autenticado, cuando hace `GET /api/v1/admin/llm-costos?desde=2026-08-01`, entonces recibe agregado diario por `provider/modelo` sin exponer prompts.
- Dado `/metrics` es scrapeado por Prometheus, entonces responde `text/plain` con métricas, protegido por `AllowOrigins` interno (no CORS público).
- Dado `/health` agregado, cuando DB está caída, entonces `status=unhealthy`, `checks.postgres=down`, `HTTP 503`.
- Dado Langfuse está deshabilitado por env `Langfuse__Enabled=false`, cuando hay llamada LLM, entonces solo se persiste en `llm_usage` y Cloud Trace, no se falla el request.

## 5. UI States

No hay UI nueva de negocio. Afecta:
- Grafana: 3 dashboards pre-creados (JSON importable).
- `/admin/llm-costos` (opcional v1.1): tabla en frontend Admin con filtro fecha y gráfico de costo acumulado - si no se hace UI, SuperAdmin usa API directa.

## 6. Business Rules

| ID | Regla | Categoría |
|----|-------|-----------|
| OBS-R001 | Todo request HTTP genera `Activity` con `http.method`, `http.route`, `user.id` (si autenticado) y `X-Correlation-Id` | Trazas |
| OBS-R002 | `X-Correlation-Id` si viene del cliente se respeta, si no se genera `TraceId` | Propagación |
| OBS-R003 | Métricas `mpm_llm_*` etiquetan `provider` (`gemini`/`openai`) y `modelo` (`gemini-2.5-pro`, `qwen3.7-g4`) | Costos |
| OBS-R004 | `llm_usage.costo_clp` se calcula con tabla `llm_model_pricing` (precio por 1K tokens por modelo) configurada en `appsettings.json` | Costos |
| OBS-R005 | `/metrics` nunca expone `PII` (no `email`, no `codigoExterno` como label) | Seguridad |
| OBS-R006 | Health checks no hacen `SELECT *` - cada módulo hace `SELECT 1` o `SELECT COUNT(*)` ligero | Performance |
| OBS-R007 | Logs JSON incluyen `Timestamp`, `Level`, `MessageTemplate`, `Properties{TraceId, SpanId, UserId, Module}` | Logs |
| OBS-R008 | Si `Langfuse__Enabled=false`, no se bloquea el flujo LLM por error de observabilidad | Resiliencia |

## 7. Non-Goals

- No se crea `terraform` para Grafana en esta feature (reusa E04).
- No se alerta por PagerDuty/Slack en v1.
- No se migra historial de logs.
- No se cambia `AnalisisBackgroundService` de `Task.Run` a Pub/Sub.

## 8. Acceptance Criteria

- [ ] `GET /api/v1/licitaciones` genera traza visible en Cloud Trace con `traceparent` en headers.
- [ ] `POST /api/v1/analisis/{id}/comercial` con Gemini deja fila en `llm_usage` y métrica `mpm_llm_calls_total{provider="gemini"}++`.
- [ ] Repetir con Qwen (switch SuperAdmin) deja `provider="openai"` y `modelo="qwen3.7-g4"`.
- [ ] `GET /metrics` expone `mpm_http_requests_total` y es scrapeable (curl con auth interna).
- [ ] `GET /health` agregado retorna 200 con `checks` cuando todo OK, 503 si Postgres caído (test con `docker stop mpm-db`).
- [ ] `GET /api/v1/admin/llm-costos?desde=2026-08-01` para SuperAdmin retorna agregado, para `Analista` 403.
- [ ] Grafana dashboard importado muestra RPS, P95, licitaciones/día, costo LLM/día sin datos falsos.
- [ ] Ningún test existente de `Analisis`, `Licitaciones`, `Mensajeria` se rompe.
- [ ] `dotnet build` 0 errores, `npm run build` y `vitest run` verdes.

## 9. Open Questions

1. ¿Grafana Cloud (gestionado) o self-host en GCE? (asumido Cloud para v1).
2. ¿Langfuse self-host o Cloud? (asumido Cloud trial, fallback a tabla `llm_usage` si no hay credenciales).
3. ¿Precio por token de Qwen G4 ya definido por proveedor? (necesario para `llm_model_pricing`).

## 10. Tasks Overview (ver `037-observabilidad-e05.tasks.md`)

- **T1** DB `V156 llm_usage` + vista costos
- **T2** OpenTelemetry SDK + ActivitySource MPM
- **T3** prometheus-net + /metrics
- **T4** Serilog JSON + correlationId
- **T5** Health checks por módulo + agregado
- **T6** Interceptor LLM (Gemini/Qwen) → `llm_usage` + traza
- **T7** Endpoint admin costos
- **T8** Dashboards Grafana JSON + runbook

Estimación: ~3 días dev + 1 día infra (sin Terraform).
