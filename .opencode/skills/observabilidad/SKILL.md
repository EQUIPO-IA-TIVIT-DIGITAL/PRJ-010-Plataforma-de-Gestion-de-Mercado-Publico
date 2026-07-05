---
name: observabilidad
description: 'Observability: three pillars (logs, metrics, traces), OpenTelemetry,
  structured logging, dashboard design, alerting rules, SLO/SLI, distributed tracing,
  log aggregation (ELK/Loki/Prometheus/Grafana). Trigger: When designing or implementing
  observability, monitoring, alerting, or distributed tracing.'
metadata:
  phase:
  - operations
  layer:
  - infrastructure
  enforcement: recommended
  depends_on: []
  consumed_by:
  - agent-backend
  - agent-fullstack
  agent_roles:
  - delivery-agent
  - control-agent
  validation_profile: documentation
  mcp_usage: optional
---

## Propósito

Diseñar la estrategia de observabilidad del sistema basada en los tres pilares (logs, métricas, traces), instrumentación con OpenTelemetry, dashboards accionables y alertas con umbrales definidos.

## Objetivo

1. ¿Qué señales (logs, métricas, traces) necesita el sistema?
2. ¿Cómo se instrumenta cada capa (API, backend, DB, frontend) con OpenTelemetry?
3. ¿Cómo se diseña logging estructurado con correlación de trazas?
4. ¿Qué dashboards y alertas son necesarias por rol?
5. ¿Cómo se definen SLOs y SLIs para cada servicio?
6. ¿Cómo se agregan y retienen logs según criticidad?

## Relación con otras skills

- `backend-api` expone endpoints que deben estar instrumentados.
- `error-handling` genera logs estructurados que esta skill consume para alertas.
- `ci-cd` despliega la infraestructura de observabilidad (Grafana, Loki, Prometheus).
- `framework-platform` define la topología que esta skill instrumenta.
- `costos-llm` se beneficia de la correlación de traces para atribución de costos.

## Qué debe hacer el agente

1. Instrumentar cada servicio con OpenTelemetry (SDK por lenguaje).
2. Escribir logs estructurados en JSON con `trace_id`, `span_id`, `service`, `level`.
3. Exportar métricas RED (Rate, Errors, Duration) por endpoint.
4. Diseñar dashboards por rol (operador, desarrollador, negocio).
5. Definir SLOs (ej: 99.9% de requests en menos de 500ms) y SLIs medibles.
6. Configurar alertas con severidad (warning, critical) y destinatarios.
7. Agregar tags de correlación: `tenant_id`, `feature_id`, `user_id`.
8. Definir política de retención por tipo de señal y entorno.

## Alcance

Incluye: instrumentación OpenTelemetry, logs estructurados, métricas RED, dashboards, alertas, SLO/SLI, tracing distribuido, agregación de logs.
No incluye: profiling de CPU/memoria avanzado, APM comercial (Datadog/Dynatrace), compliance SOC2.

## Principios

- Los tres pilares se complementan: logs para eventos, métricas para tendencias, traces para caminos.
- Toda señal debe tener `service`, `environment` y `trace_id` como tags mínimos.
- Las alertas deben tener acción asociada, no solo notificación.
- Los dashboards cuentan una historia: no son tableros de métricas crudas.
- Un SLO sin SLI medible no es operativo.
- La instrumentación no debe afectar latencia de producción (sampling adaptativo).

## Technical Design

### OpenTelemetry — .NET

```csharp
// Program.cs — OTel setup
builder.Services.AddOpenTelemetry()
    .WithTracing(t => t
        .AddAspNetCoreInstrumentation()
        .AddHttpClientInstrumentation()
        .AddSqlClientInstrumentation()
        .AddOtlpExporter(opt => opt.Endpoint = new Uri("http://otel-collector:4317")))
    .WithMetrics(m => m
        .AddAspNetCoreInstrumentation()
        .AddRuntimeInstrumentation()
        .AddOtlpExporter());
```

### Structured logging (Serilog / Node.js / Python)

```csharp
// .NET Serilog
Log.Logger = new LoggerConfiguration()
    .Enrich.WithProperty("service", "my-api")
    .Enrich.WithProperty("environment", env)
    .WriteTo.Console(new JsonFormatter())
    .CreateLogger();
```

```typescript
// Node.js — pino
const logger = pino({
  level: process.env.LOG_LEVEL || 'info',
  formatters: {
    level: (label) => ({ level: label }),
  },
  mixin: () => ({ service: 'my-api' }),
});
```

```python
# Python — structlog
import structlog
structlog.configure(
    processors=[structlog.processors.JSONRenderer()],
    context_class=dict,
    logger_factory=structlog.PrintLoggerFactory(),
)
log = structlog.get_logger(service="my-api")
```

### RED metrics per endpoint

```prometheus
# HELP http_requests_total Total HTTP requests
# TYPE http_requests_total counter
http_requests_total{service="my-api",method="GET",path="/users",status="200"}

# HELP http_request_duration_seconds Request duration
# TYPE http_request_duration_seconds histogram
http_request_duration_seconds_bucket{service="my-api",le="0.1"} 1200

# HELP http_requests_in_flight Concurrent requests
# TYPE http_requests_in_flight gauge
http_requests_in_flight{service="my-api"} 5
```

### SLO / SLI definition

| SLI | Definition | SLO Target | Measurement Window |
|-----|-----------|------------|--------------------|
| Availability | (successful requests / total requests) × 100 | ≥ 99.9% | Rolling 30 days |
| Latency (p95) | 95th percentile of request duration | ≤ 500ms | Rolling 7 days |
| Error rate | (5xx responses / total) × 100 | ≤ 0.1% | Rolling 1 hour |
| Freshness | Time since last successful data sync | ≤ 5 min | Per data source |

### Alerting rules (Prometheus)

```yaml
groups:
  - name: my-api
    rules:
      - alert: HighErrorRate
        expr: rate(http_requests_total{status=~"5.."}[5m]) / rate(http_requests_total[5m]) > 0.01
        for: 5m
        labels:
          severity: critical
          service: my-api
        annotations:
          summary: Error rate > 1% for 5 minutes

      - alert: HighLatency
        expr: histogram_quantile(0.95, rate(http_request_duration_seconds_bucket[5m])) > 0.5
        for: 10m
        labels:
          severity: warning
        annotations:
          summary: p95 latency > 500ms for 10 minutes
```

### Trace correlation

```json
{
  "timestamp": "2026-05-27T10:00:00Z",
  "level": "error",
  "service": "order-api",
  "trace_id": "abc123def456",
  "span_id": "span789",
  "tenant_id": "tenant-42",
  "user_id": "user-7",
  "message": "Payment timeout",
  "error": {
    "type": "TimeoutException",
    "stack": "..."
  }
}
```

## Preguntas guía

- ¿Cada servicio tiene `trace_id` en todos sus logs?
- ¿Las métricas RED están expuestas por endpoint y método?
- ¿Cada alerta tiene un runbook asociado?
- ¿Los dashboards responden preguntas concretas de cada rol?
- ¿Los SLOs están definidos en términos medibles?
- ¿Hay política de retención por entorno?

## Salidas esperadas

- Instrumentación OpenTelemetry configurada por servicio.
- Logs estructurados en JSON con tags de correlación.
- Métricas RED expuestas (Rate, Errors, Duration).
- Dashboards de Grafana por rol.
- Reglas de alerta con severidad y runbook.
- Tabla de SLO/SLI por servicio.

## Criterios de calidad

- 100% de servicios instrumentados con OTel.
- Logs en JSON con `trace_id`, `service`, `level` obligatorios.
- Cada endpoint expone métricas RED.
- Alertas con severidad, umbral y destinatario definidos.
- SLOs documentados y dashboard principal visible.
- Trazas distribuidas correlacionan request completo.

## Comportamiento esperado del agente

Cuando un servicio no tenga instrumentación OTel, el agente debe agregarla antes de considerar el servicio operable.
Cuando los logs sean texto plano sin `trace_id`, debe reemplazarlos por logging estructurado.
Cuando no existan dashboards, debe proponer un mínimo por rol.
Cuando no haya alertas definidas, debe crear al menos error rate + latency.

## Plantilla de respuesta

```
1. Instrumentation setup (OTel per language).
2. Log format (JSON schema with required fields).
3. RED metrics per endpoint.
4. Dashboards per role (operator, dev, business).
5. Alert rules (severity, threshold, runbook).
6. SLO/SLI table per service.
7. Retention policy per environment.
```

## Ejemplos

### Ejemplo 1 — Correlación

```
Request: POST /orders
Trace: abc123
  → API Gateway (span: gw-1)
    → Order Service (span: order-1)
      → Payment Service (span: pay-1)
      → DB Query (span: db-1)
  All logs with trace_id=abc123 are correlated in Grafana/Loki.
```

### Ejemplo 2 — SLO Burn Rate Alert

```yaml
- alert: SLOBurnRate
  expr: (1 - (successful / total)) > (1 - 0.999) * 14.4  # 99.9% SLO, 1h window
  for: 1h
  labels:
    severity: critical
```

## Checklist

- [ ] OpenTelemetry SDK configurado en cada servicio.
- [ ] Logs en JSON con `trace_id`, `span_id`, `service`, `level`.
- [ ] Métricas RED por endpoint (Rate, Errors, Duration).
- [ ] Trazas distribuidas entre servicios.
- [ ] Dashboards de Grafana por rol.
- [ ] Alertas con severity + umbral + destinatario.
- [ ] SLOs definidos con SLIs medibles.
- [ ] Política de retención por entorno y tipo de señal.
- [ ] OTel Collector desplegado como proxy de exportación.
