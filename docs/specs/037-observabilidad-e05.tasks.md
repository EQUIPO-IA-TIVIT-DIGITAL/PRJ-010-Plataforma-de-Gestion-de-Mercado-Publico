# Tasks: 037 Observabilidad Integral + Costos LLM (E05) — v1.0

**Spec:** `docs/specs/037-observabilidad-e05.feature-spec.md`
**Rama:** `037-observabilidad-e05`
**Estimación total:** ~24h dev + 6h infra/doc = 3.5 días (1 dev) / 2 días (2 devs)
**Infra requerida:** `tivit-cu010` GCP existente, no bloquea limpieza actual. Requiere OTLP endpoint (Cloud Trace) y Langfuse creds si se usa (env `Langfuse__Enabled`).

> Convenciones: migraciones `V156__` siguiente libre (tras V154/V155). Contrato `ApiResponse<T>` existente. Test Gate por capa: backend `dotnet test`, frontend `vitest` si hay UI admin.

---

## T1 — DB: `V156__Create_llm_usage.sql` (2h)
**Depende:** nada
**Work:**
- `src/MPM.Api/Database/Scripts/V156__Create_llm_usage.sql`:
  - `CREATE TABLE llm_usage (id BIGSERIAL PK, trace_id VARCHAR(32) NOT NULL, provider VARCHAR(20) NOT NULL, modelo VARCHAR(50) NOT NULL, prompt_tokens INT, completion_tokens INT, total_tokens INT GENERATED ALWAYS AS (coalesce(prompt_tokens,0)+coalesce(completion_tokens,0)) STORED, latency_ms INT, costo_clp NUMERIC(12,2), licitacion_id BIGINT NULL, workspace_id BIGINT NULL, created_at TIMESTAMPTZ DEFAULT NOW())`
  - `CREATE INDEX idx_llm_usage_trace ON llm_usage(trace_id); CREATE INDEX idx_llm_usage_provider_modelo ON llm_usage(provider, modelo); CREATE INDEX idx_llm_usage_created ON llm_usage(created_at);`
  - `CREATE TABLE llm_model_pricing (modelo VARCHAR(50) PK, precio_prompt_1k NUMERIC(10,4), precio_completion_1k NUMERIC(10,4), moneda VARCHAR(3) DEFAULT 'CLP', updated_at TIMESTAMPTZ DEFAULT NOW())` + seed `gemini-2.5-pro` (0.15/0.60 CLP 1K aprox - ajustar con precio real Vertex), `qwen3.7-g4` (precio proveedor).
  - `CREATE VIEW v_llm_costos_diarios AS SELECT date_trunc('day', created_at)::date as dia, provider, modelo, count(*) as calls, sum(total_tokens) as tokens, sum(costo_clp) as costo FROM llm_usage GROUP BY 1,2,3`
  - SP `usp_LlmUsage_Registrar(p_trace_id, p_provider, p_modelo, p_prompt_tokens, p_completion_tokens, p_latency_ms, p_licitacion_id, p_workspace_id)` calcula `costo_clp` via `llm_model_pricing`.
  - SP `usp_LlmCostos_Resumen(p_desde DATE, p_hasta DATE)` retorna `dia, provider, modelo, calls, tokens, costo`.
**Verify:**
```sql
SELECT * FROM llm_usage LIMIT 1;
INSERT INTO llm_model_pricing VALUES ('test-model', 0.1, 0.2, 'CLP', NOW());
CALL usp_LlmUsage_Registrar('abc123', 'gemini', 'test-model', 100, 200, 1200, NULL, NULL);
SELECT * FROM v_llm_costos_diarios;
```
- [ ] Migración corre limpia en `docker compose up --build` desde cero.

---

## T2 — Backend: OpenTelemetry SDK + ActivitySource MPM (6h)
**Depende:** T1
**Work:**
- `MPM.Core/Observability/MpmActivitySource.cs` : `static ActivitySource Instance = new("MPM.Api", "1.0.0")`.
- `Program.cs`:
  - `builder.Services.AddOpenTelemetry().WithTracing(b => b.AddSource("MPM.Api").AddAspNetCoreInstrumentation(o=>o.RecordException=true).AddNpgsql().AddRedisInstrumentation().AddHttpClientInstrumentation().AddOtlpExporter(o=>o.Endpoint = builder.Configuration["Otlp:Endpoint"] ?? "http://localhost:4317"))`
  - Paquetes: `OpenTelemetry.Extensions.Hosting`, `OpenTelemetry.Instrumentation.AspNetCore`, `OpenTelemetry.Instrumentation.Npgsql`, `OpenTelemetry.Exporter.OpenTelemetryProtocol`
  - Env `Otlp:Endpoint` y `Otlp:Enabled` (default false local, true en prod).
- `TenantMiddleware`: al inicio de request crea `Activity` si no existe, inyecta `X-Correlation-Id` header, propaga `traceparent`/`tracestate`.
- `LlmClientResolver`: envuelve `llm.CallAsync` con `using var activity = MpmActivitySource.Instance.StartActivity("llm.call")` + tags `llm.provider`, `llm.modelo`.
**Verify:**
```bash
dotnet build # 0 errors
curl -i http://localhost:5001/api/v1/licitaciones | grep -i traceparent
# ver traza en Jaeger local o Cloud Trace si Otlp:Enabled=true
```
- [ ] `GET /api/v1/licitaciones` genera traza con span DB visible.

---

## T3 — Backend: prometheus-net + /metrics (3h)
**Depende:** T2
**Work:**
- `Program.cs`:
  - `builder.Services.AddMetricFactory()` y `app.UseMetricServer("/metrics")` o `app.MapMetrics()` con `prometheus-net.AspNetCore`.
  - Paquete `prometheus-net.AspNetCore`.
  - Definir `MPM.Metrics.MpmMetrics` static: `http_requests_total`, `http_duration_seconds` (Histogram), `llm_calls_total`, `llm_tokens_total`, `llm_latency_seconds`, `sync_licitaciones_total`, `aclaraciones_detectadas_total`, `scraper_runs_total`.
  - Middleware `UseHttpMetrics` automático + manual `MpmMetrics.LlmCalls.WithLabels(provider, modelo).Inc()` en `LlmUsage` registrar.
- Config `Metrics:Enabled` default true, `/metrics` protegido por `AllowAnonymous` pero filtrado por `Cors` interno (no expone PII).
**Verify:**
```bash
curl http://localhost:5001/metrics | grep mpm_http_requests_total
```
- [ ] Métrica `mpm_llm_calls_total` incrementa tras análisis comercial.

---

## T4 — Backend: Serilog JSON + correlationId (3h)
**Depende:** T2
**Work:**
- `Program.cs`: reemplazar `Builder.Logging` por `Serilog`:
  - `Log.Logger = new LoggerConfiguration().ReadFrom.Configuration(builder.Configuration).Enrich.WithProperty("Application","MPM.Api").Enrich.WithTraceId().Enrich.WithSpanId().CreateLogger()`
  - Paquetes `Serilog.AspNetCore`, `Serilog.Enrichers.Trace`, `Serilog.Sinks.Console` (JSON).
  - `appsettings.json` añade `"Serilog": {"MinimumLevel":"Information", "WriteTo":[{"Name":"Console", "Args":{"formatter":"Serilog.Formatting.Compact.CompactJsonFormatter"}}]}`
- `TenantMiddleware` y `ErrorHandlingMiddleware` loguean con `Log.ForContext("CorrelationId", traceId)`.
**Verify:**
```bash
docker logs mpm-api --tail 20 | jq .TraceId
```
- [ ] Cada log JSON tiene `TraceId`/`SpanId`/`CorrelationId`.

---

## T5 — Backend: Health checks por módulo + agregado (4h)
**Depende:** nada (paralelo a T2)
**Work:**
- `Program.cs`: `builder.Services.AddHealthChecks().AddNpgSql(connectionString, name="postgres").AddRedis(redisConn, name="redis").AddCheck<AnalisisHealthCheck>("analisis").AddCheck<CensoHealthCheck>("censo").AddCheck<PropuestasHealthCheck>("propuestas")`
- Cada módulo `MPM.Modules.X/Health/XHealthCheck.cs : IHealthCheck` hace `SELECT 1` ligero.
- `app.MapHealthChecks("/health", new HealthCheckOptions{ResponseWriter=UIResponseWriter.WriteHealthCheckUIResponse})` y `/health/licitaciones` etc mapeando tags.
- Contratos `GET /health/{modulo}` no requieren auth, `GET /health` agregado requiere auth? No, público pero sin detalle PII.
**Verify:**
```bash
curl http://localhost:5001/health | jq .status
curl http://localhost:5001/health/licitaciones | jq
docker stop mpm-db && curl http://localhost:5001/health -w "%{http_code}"
```
- [ ] `/health` 200 OK normal, 503 si Postgres caído.

---

## T6 — Backend: Interceptor LLM → llm_usage + traza (4h)
**Depende:** T1, T2
**Work:**
- `MPM.Shared/Services/LlmUsageService.cs` con `RegistrarAsync(traceId, provider, modelo, promptTokens, completionTokens, latencyMs, licitacionId, workspaceId)` llama `usp_LlmUsage_Registrar`.
- `VertexGeminiClient` y `OpenAiCompatClient` inyectan `LlmUsageService`, miden `Stopwatch`, capturan `usage.prompt_token_count` / `usage.candidates_token_count` (Vertex) o `usage.prompt_tokens/completion_tokens` (OpenAI), llaman `RegistrarAsync` sin bloquear el flujo (try/catch log warning si falla).
- Tags de traza ya en T2.
**Verify:**
```bash
# Hacer análisis comercial QA con Gemini
curl -X POST http://localhost:5001/api/v1/analisis/1/comercial -H "Authorization: Bearer $TOKEN"
psql -c "SELECT provider, modelo, prompt_tokens, costo_clp FROM llm_usage ORDER BY id DESC LIMIT 1"
# Repetir tras switch a Qwen en /admin/ia
```
- [ ] Dos filas con provider gemini/openai y costo calculado.

---

## T7 — Backend: Endpoint admin costos (2h)
**Depende:** T1, T6
**Work:**
- `MPM.Modules.Administracion/Controllers/AdminLlmCostosController.cs`:
  - `GET /api/v1/admin/llm-costos?desde=YYYY-MM-DD&hasta=YYYY-MM-DD` `[Authorize(Roles="SuperAdmin")]`
  - Llama `usp_LlmCostos_Resumen` y retorna `ApiResponse<List<LlmCostoDiaDto>>`
  - `LlmCostoDiaDto { dia, provider, modelo, calls, tokens, costo }`
**Verify:**
```bash
curl -H "Authorization: Bearer $SUPERADMIN" "http://localhost:5001/api/v1/admin/llm-costos?desde=2026-08-01"
# 200 OK para SuperAdmin, 403 para Analista
```
- [ ] SuperAdmin ve agregado, Analista 403.

---

## T8 — Infra/Docs: Dashboards Grafana + Runbook (4h)
**Depende:** T2, T3
**Work:**
- `docs/observabilidad/dashboards/`:
  - `grafana-operativo.json` - panel RPS, P95 latency, error rate (query `mpm_http_requests_total`, `mpm_http_duration_seconds`)
  - `grafana-negocio.json` - licitaciones ingestadas/día, aclaraciones/día, scraper success rate
  - `grafana-llm.json` - calls/día por provider, costo acumulado, latencia p50/p95
- `docs/observabilidad/runbook.md` - cómo importar dashboards, OTLP endpoint prod, troubleshooting `TraceId` no aparece, `Langfuse__Enabled`.
- `docker-compose.yml` añade vars `Otlp__Enabled=false`, `Otlp__Endpoint=http://otel-collector:4317`, `Langfuse__Enabled=false`.
**Verify:**
- [ ] Dashboards importables en Grafana Cloud sin datos falsos (queries existen).
- [ ] Runbook sigue `docs/runbook-produccion.md` estilo.

---

## Criterios de cierre del feature

- [ ] T1-T8 completos, `dotnet build` 0 errores, `npm run build` y `vitest run` verdes.
- [ ] `/metrics` scrapeable, `/health` 200/503, `llm_usage` poblada tras análisis Gemini y Qwen.
- [ ] Dashboards importables, runbook en `docs/observabilidad/runbook.md`.
- [ ] Ningún test existente roto.
- [ ] PR `037-observabilidad-e05` con spec + tasks + código, listo para review control-agent.

