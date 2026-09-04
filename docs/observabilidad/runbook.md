# Runbook Observabilidad 037 — Serilog + OTel + Metrics + LLM Costos

**Rama:** `037-observabilidad-e05` (037-A Serilog/Health + 037-B OTel/Metrics + **037-C LLM Costos**)
**Fecha:** 2026-08-24
**Owner:** TIVIT Foundry / SRE MPM

Este runbook cubre la operación diaria de la observabilidad integral de MPM: cómo importar dashboards, dónde vive cada señal (logs, trazas, métricas, costos), OTLP en prod, troubleshooting de TraceId y el flag de Langfuse.

---

## 1. Importar dashboards Grafana (3 archivos)

**Archivos:** `docs/observabilidad/dashboards/grafana-{operativo,negocio,llm}.json`

### Grafana Cloud

1. Grafana Cloud → Dashboards → **Import** → **Upload JSON file**
2. Selecciona `grafana-operativo.json` → asigna `DS_PROMETHEUS` a tu Prometheus datasource (Mimir / Cloud Prometheus que scrapea `/metrics` de MPM.Api)
3. Repite para `grafana-negocio.json` y `grafana-llm.json`
4. Verifica que cada panel muestre `No data` → luego datos tras el primer scrape (30s)

### Grafana self-host en GCE

```bash
# Copiar JSON al volumen de provisioning
scp docs/observabilidad/dashboards/*.json gce-grafana:/var/lib/grafana/dashboards/
# O vía API
curl -X POST http://grafana:3000/api/dashboards/db \
  -H "Authorization: Bearer $GRAFANA_TOKEN" \
  -H "Content-Type: application/json" \
  -d @docs/observabilidad/dashboards/grafana-operativo.json
```

**Datasource esperado:** Prometheus con `url: http://mpm-api:80/metrics` (prod) o `http://localhost:5001/metrics` (local). Scrape interval 15s. No requiere auth (endpoint interno, sin CORS público). Verifica:

```bash
curl http://localhost:5001/metrics | grep mpm_http_requests_total
# Debe listar HELP/TYPE + series con labels method/route/status
```

**Métricas disponibles (OBS-R005 sin PII):**

- `mpm_http_requests_total{method,route,status}` — Counter
- `mpm_http_duration_seconds{method,route,status}` — Histogram 5ms..10s
- `mpm_llm_calls_total{provider,modelo}` — Counter GEMINI/OPENAI
- `mpm_llm_tokens_total{provider,modelo,tipo}` — tipo prompt|completion
- `mpm_llm_latency_seconds{provider,modelo}` — Histogram 50ms..25s
- `mpm_sync_licitaciones_total{estado}` / `mpm_aclaraciones_detectadas_total{tipo}` / `mpm_scraper_runs_total{estado}`

---

## 2. OTLP endpoint prod (Cloud Trace)

**GCP proyecto:** `tivit-cu010` — Cloud Trace vía OTLP gRPC.

- Env prod: `Otlp__Enabled=true` + `Otlp__Endpoint=https://otel-collector.tivit-cu010:4317` (o el collector de tu infra E04 WIF)
- Local / docker-compose: `Otlp__Enabled=false` + `Otlp__Endpoint=http://localhost:4317` (no falla si collector no existe — exporter solo se registra cuando `Enabled=true`)
- Si `Otlp:Enabled=false`, las trazas siguen vivas en proceso (ActivitySource MPM.Api) pero no se exportan; no bloquea arranque (mitiga riesgo OTLP endpoint inexistente)

Verificar traza:

```bash
curl -i http://localhost:5001/api/v1/licitaciones?montoDesde=50000000 | grep -i traceparent
# header traceparent: 00-<traceId>-<spanId>-01
# Ver en Cloud Trace: console.cloud.google.com/traces/list?project=tivit-cu010
```

**Activity / tags emitidos:**

- `llm.call` (VertexGeminiClient / OpenAiCompatClient) con `llm.provider`, `llm.modelo`, `llm.prompt_tokens`, `llm.latency_ms`
- `llm.resolve` (LlmClientResolver) con `llm.provider`, `llm.modelo`, `llm.fuente`
- Cada request HTTP con `http.method`, `http.route`, `user.id` (si autenticado, OBS-R001), `correlationId`, `spanId`

---

## 3. Troubleshooting TraceId / CorrelationId

**Síntoma:** Log JSON sin `TraceId` o `SpanId`, o `X-Correlation-Id` no coincide con `traceparent`.

- Flujo esperado: cliente envía `X-Correlation-Id: abc123` → `CorrelationIdMiddleware` lo sanitiza (^[a-zA-Z0-9_-]{8,36}$), lo propaga como `correlationId` y como `traceId` fallback; si no viene, se usa `Activity.Current.TraceId` o `Guid.NewGuid()`. Respuesta siempre devuelve `X-Correlation-Id`, `X-Trace-Id`, `traceparent`.
- Ver en logs: `docker logs mpm-api --tail 20 | jq .TraceId` — cada línea JSON debe traer `TraceId`, `SpanId`, `CorrelationId`, `UserId` (si autenticado) y `Module: MPM.Api` (OBS-R007)
- Ver en respuesta: `curl -i http://localhost:5001/health | grep -E "X-Correlation|X-Trace|traceparent"`
- Si `TraceId` vacío en logs pero sí en header: revisar `CorrelationIdMiddleware` order — debe ir antes de `UseSerilogRequestLogging` (ya está). Si sigue vacío, verificar `EnrichDiagnosticContext` en Program.cs: debe pushear `TraceId`, `SpanId`, `UserId`, `Module`.
- Si `UserId` no aparece logueado: verificar claim `user_id` en JWT (AuthHandler lo emite). `TenantMiddleware` lee `user_id` | `tenant_id` | `username` | `role`.
- **Langfuse deshabilitado no oculta TraceId** — solo afecta llm_usage vs Langfuse. Ver siguiente sección.

---

## 4. Langfuse flag (feature-flag sin bloqueo)

- Env: `Langfuse__Enabled=false` (default) / `Langfuse__Enabled=true`
- También `Langfuse:Enabled` en `appsettings.json` (mismo efecto)
- Cuando `false`: cada llamada LLM solo persiste en `llm_usage` (tabla + métricas) y en Cloud Trace (`llm.call` activity). No se intenta contactar Langfuse, no se bloquea flujo LLM (OBS-R008)
- Cuando `true`: además se podría enviar trace a Langfuse (hooks futuros); si Langfuse falla, la llamada LLM no falla (try/catch LogWarning)
- `LlmUsageService.RegistrarAsync` nunca hace throw — siempre try/catch + `LogWarning`; el `costo_clp` se calcula en SP via `llm_model_pricing`, no en cliente

**No tocar** `Langfuse__Enabled` en prod hasta que exista cuenta Langfuse aprovisionada. Dejar `false`.

---

## 5. Costos LLM — tabla y endpoint admin

**Tabla:** `llm_usage` (V156) + `llm_model_pricing` + vista `v_llm_costos_diarios`

```sql
-- Ver última inserción
SELECT trace_id, provider, modelo, prompt_tokens, completion_tokens, total_tokens, latency_ms, costo_clp, created_at
FROM llm_usage ORDER BY id DESC LIMIT 5;

-- Ver pricing vigente (CLP por 1K tokens)
SELECT modelo, precio_prompt_1k, precio_completion_1k FROM llm_model_pricing;

-- Agregado diario (vista)
SELECT * FROM v_llm_costos_diarios ORDER BY dia DESC LIMIT 10;

-- Resumen por rango
SELECT * FROM usp_LlmCostos_Resumen('2026-08-01'::date, '2026-08-24'::date);
```

**Endpoint:** `GET /api/v1/admin/llm-costos?desde=YYYY-MM-DD&hasta=YYYY-MM-DD`

- Auth: `SuperAdmin` only (403 para otros roles, 401 sin token)
- Retorna `ApiResponse<List<LlmCostoDiaDto>>` con `{ dia, provider, modelo, calls, tokens, costo }`
- Sin `desde`/`hasta`: últimos 30 días. Rango máx 365 días.

```bash
# SuperAdmin
curl -H "Authorization: Bearer $SUPERADMIN_TOKEN" \
  "http://localhost:5001/api/v1/admin/llm-costos?desde=2026-08-01&hasta=2026-08-24" | jq

# Analista -> 403
curl -H "Authorization: Bearer $ANALISTA_TOKEN" \
  "http://localhost:5001/api/v1/admin/llm-costos" -i | grep 403
```

**Validar 037-C E2E:**

```bash
# 1. Hacer análisis comercial QA con Gemini (provider activo gemini por defecto)
curl -X POST http://localhost:5001/api/v1/analisis/1/comercial -H "Authorization: Bearer $TOKEN"
psql -c "SELECT provider, modelo, prompt_tokens, costo_clp FROM llm_usage ORDER BY id DESC LIMIT 1"
# -> provider=gemini modelo=gemini-2.5-pro costo>0

# 2. Switch a Qwen (SuperAdmin via /api/v1/admin/ia o SystemConfig)
curl -X PUT http://localhost:5001/api/v1/admin/ia -H "Authorization: Bearer $SUPERADMIN" -d '{"provider":"openai","model":"qwen3.7-g4"}'
# Repetir análisis -> verificar segunda fila provider=openai modelo=qwen3.7-g4
psql -c "SELECT provider, modelo FROM llm_usage ORDER BY id DESC LIMIT 2"

# 3. Metrics
curl http://localhost:5001/metrics | grep mpm_llm_calls_total
# -> mpm_llm_calls_total{provider="gemini",modelo="gemini-2.5-pro"} 1

# 4. Health
curl http://localhost:5001/health | jq .status
docker stop mpm-db && curl http://localhost:5001/health -w "%{http_code}\n" ; docker start mpm-db
# -> 503 cuando DB caída, 200 normal
```

---

## 6. Env vars y docker-compose

**docker-compose.yml (local):**

```yaml
environment:
  - Otlp__Enabled=false
  - Otlp__Endpoint=http://otel-collector:4317
  - Langfuse__Enabled=false
  # Metrics:Enabled=true es default en appsettings.json (no env requerido)
```

**docker-compose.prod.yml (GCP VM / Cloud Run):**

```yaml
environment:
  - Otlp__Enabled=true
  - Otlp__Endpoint=https://otel-collector.tivit-cu010:4317
  - Langfuse__Enabled=false
```

**appsettings.json:**

```json
"Otlp": { "Enabled": false, "Endpoint": "http://localhost:4317" },
"Metrics": { "Enabled": true },
"Langfuse": { "Enabled": false }
```

Sin `Langfuse__Enabled` o `Otlp__Enabled`, el flag defaultea a `false` (seguro). Ver `Program.cs` línea OTLP Enabled check y `LlmUsageService` flag.

---

## 7. Qué NO se toca / deuda

- **Serilog y Health de 037-A** no se tocan (Serilog JSON + correlationId + health checks por módulo + MpmActivitySource vacío ya aprobados).
- **OTel SDK de 037-B** no se rewirea (solo se añade `llm.call` child + `user.id` tags).
- **AnalisisBackgroundService** sigue como `Task.Run` (B-004); no se migra a Pub/Sub en 037.
- **Alerting** (PagerDuty/Slack) queda fuera de v1 (solo dashboards lectura).
- **Terraform** Grafana reuse E04 WIF.

Si algo falla en OTel o llm_usage, el flujo de negocio sigue (OBS-R008). Revertir flag: setear `Otlp__Enabled=false` y redeploy sin rebuild.

---

## Contactos

- SRE: #ops-mpm / #sre-tivit
- Dev LLM: `MPM.Shared.Services.LlmUsageService`
- DBA: `V156__Create_llm_usage.sql` — tabla nueva, no bloquea

