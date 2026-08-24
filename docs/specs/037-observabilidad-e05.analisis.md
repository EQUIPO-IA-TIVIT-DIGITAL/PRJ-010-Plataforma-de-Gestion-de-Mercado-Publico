# Análisis de Impacto - 037 Observabilidad Integral (E05)

**Fecha:** 2026-08-24
**Rama:** `037-observabilidad-e05`
**Analista:** Orchestrator TIVIT Foundry (control + delivery)
**Objetivo:** Determinar qué partes tocamos, qué riesgos introducimos y si el scope/tasks de `037-observabilidad-e05.feature-spec.md` es válido antes del GO/NO GO.

---

## 1. Estado actual auditado

| Capa | Existe hoy | Falta para 037 |
|------|------------|----------------|
| **Logs** | `ILogger` disperso (163 usos), `ErrorHandlingMiddleware` con `LogError`, `appsettings.json` `Logging:Information` básico a stdout | Serilog JSON estructurado, `correlationId`/`TraceId` en cada log, sink Cloud Logging |
| **Trazas** | 0 - ningún `ActivitySource`, ningún `AddOpenTelemetry`, ningún `W3C traceparent` | OTel SDK + `MpmActivitySource("MPM.Api")` + propagación via `TenantMiddleware` + hijos `Npgsql`/`Redis`/`HttpClient` |
| **Métricas** | 0 - ningún `/metrics`, ningún `prometheus-net` | `prometheus-net.AspNetCore` + 8 métricas `mpm_*` + `/metrics` endpoint |
| **Health** | Parcial: solo `GET /health/licitaciones` existe en `docs/api-first/licitaciones.md` pero no está cableado en `Program.cs`. Ningún `AddHealthChecks()` | `AddHealthChecks().AddNpgSql().AddRedis().AddCheck(...)` por módulo + `/health` agregado y `/health/{modulo}` |
| **LLM** | `LlmClientResolver` → `VertexGeminiClient`/`OpenAiCompatClient` ya resuelve `provider/modelo` por request (033), persiste `analisis.modelo_usado` pero no tokens/costo. Ninguna tabla `llm_usage` | Tabla `llm_usage` + `llm_model_pricing` + vista `v_llm_costos_diarios` + `LlmUsageService` interceptor + endpoint `admin/llm-costos` |
| **Infra** | `docker-compose.yml` (db, redis, api, web), `docker-compose.prod.yml` con `cloudsql-proxy`. Sin OTel collector, sin Grafana | `Otlp:Endpoint` env + `Langfuse__Enabled` flag, dashboards JSON importables |
| **Frontend** | `apiClient.ts` maneja 401 y throw `ApiError`, no envía `X-Correlation-Id` | Opcional: admin tabla costos (si se hace UI) |

**Conclusión:** 037 no pisa lógica de negocio. Toca solo *cross-cutting* (Program.cs, middlewares, servicios transversales, DB migración). Riesgo funcional bajo, riesgo de regresión en wiring DI y middleware order medio.

---

## 2. Matriz de impacto por archivo

| Task | Archivos tocados | Tipo de cambio | Riesgo |
|------|------------------|----------------|--------|
| **T1 V156 llm_usage** | `src/MPM.Api/Database/Scripts/V156__Create_llm_usage.sql` (nuevo), `MPM.sln` embedded resource | Solo DDL nuevo, sin ALTER de tablas existentes | **Bajo** - si falla, solo faltan métricas LLM, no rompe flujo. Idempotente. |
| **T2 OTel SDK** | `src/MPM.Api/Program.cs` (20 líneas), `src/MPM.Core/Observability/MpmActivitySource.cs` (nuevo), `src/MPM.Core/Middleware/TenantMiddleware.cs` (+15 líneas), `src/MPM.Core/SystemConfig/LlmClientResolver.cs` (+activity) | Añade `AddOpenTelemetry()` antes de `AddControllers()`, orden importa | **Medio** - si OTLP endpoint mal configurado y `Enabled=true`, puede tirar excepción en arranque. Mitigación: `Otlp:Enabled=false` default local, `try/catch` alrededor. |
| **T3 Metrics** | `Program.cs` (+10 líneas), `src/MPM.Core/Observability/MpmMetrics.cs` (nuevo, 40 líneas), `src/MPM.Shared/Services/VertexGeminiClient.cs` (+1 línea `Inc()`), `OpenAiCompatClient.cs` (+1) | `app.MapMetrics()` o `UseMetricServer` | **Bajo** - `/metrics` es solo lectura. Único riesgo es exponer PII como label (mitigado por OBS-R005). |
| **T4 Serilog** | `Program.cs` (reemplaza `Builder.Logging`), `appsettings.json` (+15 líneas Serilog), `TenantMiddleware` (+log enrich), `ErrorHandlingMiddleware` (+Serilog) | Reemplaza `ILogger` factory, no cambia firmas | **Medio** - si `Serilog` mal configurado, no hay logs. Mitigación: fallback a `Console` si `Serilog:WriteTo` vacío. Probado en Docker local antes. |
| **T5 Health** | `Program.cs` (+15 líneas `AddHealthChecks`), `src/MPM.Modules.X/Health/*.cs` ×5 (cada 15 líneas, `SELECT 1`), `docker-compose.yml` healthcheck ya existe para db/redis, no se toca | `MapHealthChecks("/health")` antes de `MapControllers` | **Bajo** - si un check falla, solo responde 503, no tumba API. Health no requiere auth hoy, se mantiene así. |
| **T6 Interceptor LLM** | `src/MPM.Shared/Services/LlmUsageService.cs` (nuevo, 50 líneas), `VertexGeminiClient.cs` (+10), `OpenAiCompatClient.cs` (+10), `src/MPM.Modules.Analisis/Services/GeminiService.cs` (usa resolver, ya instrumentado) | Envuelve `GenerarContenidoAsync` con `Stopwatch` + `try { registrar } catch { log warning }` | **Medio-Bajo** - si `llm_usage` insert falla, no debe fallar la llamada LLM (OBS-R008). Mitigación: `try/catch` + log. |
| **T7 Endpoint admin** | `src/MPM.Modules.Administracion/Controllers/AdminLlmCostosController.cs` (nuevo, 60 líneas), `AdminLlmCostosService.cs` (nuevo) | Nuevo `GET /api/v1/admin/llm-costos` con `[Authorize(Roles=SuperAdmin)]` | **Bajo** - endpoint aislado, 403 para no-SuperAdmin, no toca otros módulos. |
| **T8 Dashboards** | `docs/observabilidad/dashboards/*.json` (3 archivos), `docs/observabilidad/runbook.md` (nuevo), `docker-compose.yml` (+3 env vars), `docker-compose.prod.yml` (+mismo) | Solo docs + env | **Bajo** - ningún código. |
| **No tocado** | `src/mpm-web/*` (salvo opcional admin UI), `src/MPM.Modules.Licitaciones/*` lógica de negocio, `src/MPM.Modules.Propuestas/*`, scraper, sync, alertas | - | 0 |

**Total líneas estimadas:** ~350 líneas backend + 150 SQL + 100 docs = 600 líneas. 0 rewrites, 0 breaking changes.

---

## 3. Validación de scope/tasks propuestos

### Scope incluido está correcto, pero conviene dividirlo

El `feature-spec.md` original lista 8 inclusiones y la estimación 24h + 6h = 3.5 días es realista para 1 dev senior. Sin embargo, para minimizar riesgo de regresión en `Program.cs` (que es single point of failure), **recomiendo dividir 037 en 3 specs secuenciales** en lugar de un solo big bang. Validación:

| Spec propuesta | Tasks | Scope validado | Por qué dividir |
|----------------|-------|----------------|----------------|
| **037-A Fundamentos** | T4 Serilog + T5 Health + parte de T2 (solo `TenantMiddleware` correlation sin OTel) | `Incluido 1 parcial` | Toca `Program.cs` wiring base. Si falla, falla todo. Entregar solo logs+health primero permite validar Docker sin OTLP externo. Es el cimiento. **Estimado 1 día, riesgo bajo, valor inmediato (health para prod).** |
| **037-B Trazas y Métricas** | T2 OTel completo + T3 Metrics | `Incluido 1 completo` | Toca `Program.cs` de nuevo pero ya con base estable de 037-A. Requiere OTLP endpoint (Cloud Trace) - depende de infra. Si OTLP no está, se mergea con `Enabled=false` sin romper. **Estimado 1 día, riesgo medio (OTLP).** |
| **037-C Costos LLM y Dashboards** | T1 DB + T6 Interceptor + T7 Endpoint + T8 Dashboards | `Incluido 2 completo` | Toca DB (V156) y LLM clients. Es el valor de negocio directo (saber cuánto cuesta Gemini/Qwen). Depende de T2 para trazas pero puede ir en paralelo a 037-B. **Estimado 1.5 días, riesgo bajo.** |

**Alternativa keep-as-one:** Si prefieres un solo PR, el riesgo es que un fallo en OTel bloquee también `llm_usage`. Dividir permite GO/NO GO parcial: si 037-A pasa QA en staging, ya puedes desplegar health/logs a prod mientras 037-B sigue en review.

**Mi recomendación:** dividir en 3 specs con 3 branches `037-A`, `037-B`, `037-C` secuenciales (A→B, A→C en paralelo). Te deja dar GO/NO GO por spec, no todo o nada. El spec actual 037 lo dejo como *parent* y creo 3 hijos si apruebas.

### Tasks o scope faltante / sobrante

- **Falta T0 preflight:** validar que `V155` (NoOp) no colisione con `V156` numeración (ya está libre, V155 existe como NoOp, V156 es siguiente - OK).
- **Sobra:** `Langfuse` en spec original. Hoy no tienes cuenta Langfuse y ya tienes `llm_usage` + Cloud Trace. Propongo **sacar Langfuse de v1** y dejar solo `Langfuse__Enabled` flag documentado para futuro. Simplifica T6.
- **Falta:** test de regresión explícito: `dotnet test` debe pasar sin Docker para backend, y `curl /health` en CI. Lo añado a cada spec.
- **Scope correcto:** no tocar frontend (salvo opcional admin UI) es correcto - el valor está en backend.

### Estimación ajustada

Original 24h dev es correcto. Dividido:
- 037-A: 7h (T4 3h + T5 4h)
- 037-B: 7h (T2 6h + T3 3h, pero 2h solapan)
- 037-C: 10h (T1 2h + T6 4h + T7 2h + T8 4h con doc)
- Total igual 24h, pero con 2 checkpoints intermedios.

---

## 4. Riesgos y mitigaciones

| Riesgo | Probabilidad | Impacto | Mitigación |
|--------|--------------|---------|------------|
| OTLP endpoint no existe en `tivit-cu010` y `Otlp:Enabled=true` tira excepción en arranque | Media | Alto (API no arranca) | Default `Otlp:Enabled=false` local/docker, solo `true` en prod con env var. Try/catch alrededor de `AddOtlpExporter`. |
| Serilog JSON rompe `docker logs` legibilidad | Baja | Medio | Mantener `CompactJsonFormatter` + `Console` sink, no file sink. Logs siguen siendo `jq` parseables. |
| `llm_usage` insert falla por FK o timeout y hace fallar análisis comercial | Baja | Alto (pierdes análisis) | T6 con `try/catch` + `LogWarning`, nunca `throw`. OBS-R008. |
| `/metrics` expone `codigoExterno` como label y cardinalidad explota | Baja | Medio | OBS-R005: solo `provider`, `modelo`, `route` templada (no id). Rule en `MpmMetrics`. |
| Health check `SELECT 1` sobrecarga DB si se scrapea cada 5s | Baja | Bajo | Health cache 10s, no cada request. |
| `V156` corre en prod con 180k licitaciones y bloquea tabla | Baja | Medio | `CREATE TABLE llm_usage` es nueva, no toca `licitaciones`. 0 downtime. |

---

## 5. Plan de pasos dividido en specs (propuesta para GO/NO GO)

**Opción recomendada (3 specs secuenciales):**

**Spec 037-A Fundamentos (1 día):**
- Branch `037-A-fundamentos`
- Entregables: `V156` vacía? No, V156 va en C. A solo toca `Program.cs` Serilog + Health + `MpmActivitySource` vacío + `TenantMiddleware` correlation.
- Gates: `dotnet build`, `docker compose up`, `curl /health` 200/503, `docker logs | jq .CorrelationId`.

**Spec 037-B Trazas y Métricas (1 día, después de A):**
- Branch `037-B-trazas-metricas` desde `037-A`
- Entregables: OTel SDK completo, `prometheus-net`, `MpmMetrics`, `/metrics`.
- Gates: traza visible en Jaeger local o Cloud Trace header `traceparent`, `curl /metrics | grep mpm_`.

**Spec 037-C Costos LLM y Dashboards (1.5 días, puede ir en paralelo a B tras A):**
- Branch `037-C-costos-llm` desde `037-A`
- Entregables: `V156 llm_usage`, `LlmUsageService`, interceptor, endpoint admin, 3 dashboards JSON.
- Gates: análisis Gemini deja fila en `llm_usage`, switch a Qwen deja fila con otro provider, `GET /admin/llm-costos` 200/403, Grafana importable.

**Opción alternativa (1 spec big bang) - la que ya tienes:**
- Si prefieres un solo PR, mantengo `037-observabilidad-e05` tal cual con 8 tasks. Ventaja: un solo review. Desventaja: si OTel falla, bloquea costos LLM.

**Mi validación:** el scope/tasks que coloqué en `037-observabilidad-e05.feature-spec.md` y `tasks.md` **son válidos y completos** (no falta nada crítico, no sobra salvo Langfuse que recomiendo sacar de v1). La división en 3 specs no cambia el scope total, solo reduce riesgo de deploy.

---

## 6. Qué tocaríamos exactamente (resumen para GO/NO GO)

**Tocamos:** `Program.cs` (3 veces), `TenantMiddleware`, `ErrorHandlingMiddleware`, `MpmActivitySource`, `MpmMetrics`, `LlmUsageService`, `VertexGeminiClient`/`OpenAiCompatClient` (2 líneas cada uno), `V156` migración, `AdminLlmCostosController`, `appsettings.json`, `docker-compose.yml` (3 env vars), `docs/observabilidad/*`. **No tocamos** lógica de licitaciones, análisis, mensajeria, frontend (salvo opcional admin), scraper, sync.

**No hay rewrite.** Es *aditivo* y *feature-flagged* (`Otlp:Enabled`, `Langfuse__Enabled`). Se puede desplegar a prod con flags en `false` y 0 impacto, luego habilitar progresivo.

**¿Procedemos con 3 specs (A→B→C) o con 1 big bang 037?** Dame el GO/NO GO y el formato que prefieres, arranco T1 (V156) inmediatamente.
