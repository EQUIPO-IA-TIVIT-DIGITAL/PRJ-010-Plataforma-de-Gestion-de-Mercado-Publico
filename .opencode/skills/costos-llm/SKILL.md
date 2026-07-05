---
name: costos-llm
description: 'LLM cost management: token cost tracking per tenant, model tiering (cheap
  vs expensive), semantic caching, response caching, prompt compression, batch vs streaming
  cost analysis, budget alerts, cost attribution per feature. Trigger: When designing
  or optimizing LLM token costs for agentic applications.'
metadata:
  phase:
  - operations
  layer:
  - design
  enforcement: optional
  depends_on: []
  consumed_by:
  - agent-backend
  - agent-fullstack
  agent_roles:
  - design-agent
  - control-agent
  validation_profile: documentation
  mcp_usage: optional
---

## Propósito

Diseñar la estrategia de gestión de costos de modelos LLM en aplicaciones agénticas: atribución por tenant y feature, caching inteligente, tiering de modelos, compresión de prompts y alertas presupuestarias.

## Objetivo

1. ¿Cómo se mide y atribuye el costo de tokens por tenant y feature?
2. ¿Qué estrategias de caching (semántico y de respuesta) reducen costos?
3. ¿Cómo se selecciona el modelo adecuado por tarea (tiering)?
4. ¿Cómo se comprimen prompts sin perder calidad?
5. ¿Cuándo conviene batch vs streaming desde el punto de vista de costo?
6. ¿Cómo se configuran alertas de presupuesto y thresholds?

## Relación con otras skills

- `observabilidad` proporciona traces y métricas donde se etiquetan costos por tenant y feature.
- `framework-platform` define tagging de recursos y costos por workload.
- `framework-data-memory-compliance` define qué datos personales deben excluirse de logs de tokens.
- `backend-api` implementa los endpoints que consumen LLM y necesitan tracking.

## Qué debe hacer el agente

1. Instrumentar cada llamada LLM con tags de `tenant_id`, `feature_id`, `model`, `prompt_tokens`, `completion_tokens`.
2. Registrar costo por llamada usando precio por token del modelo en uso.
3. Implementar semantic cache (similitud coseno en embeddings) para queries duplicadas o similares.
4. Definir tiering de modelos: tareas críticas → modelo grande (Sonnet/4o), tareas simples → modelo pequeño (Haiku/Mini).
5. Aplicar prompt compression dinámica (eliminar historial, resumir contexto).
6. Configurar alertas de presupuesto: warning al 80%, critical al 100%.
7. Generar reportes de costos por feature, tenant y modelo.
8. Evaluar batch vs streaming: batch más barato si latencia no es crítica.

## Alcance

Incluye: token tracking, caching semántico, tiering de modelos, compresión de prompts, batch vs streaming, budget alerts, reportes de costo.
No incluye: negociación de precios con proveedores, fine-tuning propio, modelos open-source self-hosted.

## Principios

- Cada token cuesta dinero real: medir antes de optimizar.
- El modelo más caro solo para tareas que realmente lo necesitan.
- El cache semántico es la herramienta de mayor impacto en reducción de costos.
- La atribución por tenant permite facturar y detectar abusos.
- Las alertas deben ser por tenant y globales.
- Prompt compression debe medirse: no sacrificar calidad por costo.

## Technical Design

### Token tracking (middleware pattern)

```typescript
// Node.js — track LLM costs per call
async function trackLLMCall(params: {
  tenantId: string;
  featureId: string;
  model: string;
  promptTokens: number;
  completionTokens: number;
}) {
  const cost = getCostPerToken(params.model);
  const totalCost = (params.promptTokens * cost.input + params.completionTokens * cost.output) / 1_000_000;

  await db.query(
    `INSERT INTO llm_costs (tenant_id, feature_id, model, prompt_tokens, completion_tokens, cost, timestamp)
     VALUES ($1, $2, $3, $4, $5, $6, NOW())`,
    [params.tenantId, params.featureId, params.model, params.promptTokens, params.completionTokens, totalCost]
  );
}
```

```python
# Python — decorator for cost tracking
def track_llm_cost(tenant_id: str, feature_id: str):
    def decorator(func):
        async def wrapper(*args, **kwargs):
            result = await func(*args, **kwargs)
            cost = (result.usage.prompt_tokens * PRICE_INPUT +
                    result.usage.completion_tokens * PRICE_OUTPUT) / 1_000_000
            await log_cost(tenant_id, feature_id, result.model, cost)
            return result
        return wrapper
    return decorator
```

### Model tiering matrix

| Tier | Models | Use Case | Cost Multiplier |
|------|--------|----------|----------------|
| Premium | Claude Sonnet 4, GPT-4o | Complex reasoning, code gen, analysis | 1× (baseline) |
| Standard | Claude Haiku, GPT-4o-mini | Classification, extraction, summarization | 0.1× - 0.2× |
| Economy | Claude Instant, GPT-3.5 | Simple routing, keyword extraction | 0.02× - 0.05× |
| Free | Embeddings (text-3-small) | Semantic cache keys, vector search | ~0.001× |

### Semantic cache

```python
# Python — semantic cache with embeddings
import numpy as np
from openai import OpenAI

class SemanticCache:
    def __init__(self, similarity_threshold: float = 0.92):
        self.threshold = similarity_threshold
        self.cache: dict[str, tuple[np.ndarray, str]] = {}

    async def get(self, query: str, client: OpenAI) -> str | None:
        query_embedding = await client.embeddings.create(input=query, model="text-embedding-3-small")
        query_vec = np.array(query_embedding.data[0].embedding)

        for cached_text, (cached_vec, response) in self.cache.items():
            similarity = np.dot(query_vec, cached_vec) / (np.linalg.norm(query_vec) * np.linalg.norm(cached_vec))
            if similarity >= self.threshold:
                return response
        return None

    async def set(self, query: str, response: str, client: OpenAI):
        embedding = await client.embeddings.create(input=query, model="text-embedding-3-small")
        self.cache[query] = (np.array(embedding.data[0].embedding), response)
```

### Prompt compression

```typescript
// Strategy: keep system prompt, compress conversation history
function compressPrompt(messages: Message[], maxTokens: number): Message[] {
  const systemMsg = messages.filter(m => m.role === 'system');
  const history = messages.filter(m => m.role !== 'system');

  // Summarize old history into a single message
  if (estimateTokens(messages) > maxTokens) {
    const summary = `Previous conversation summary: ${summarize(history.slice(0, -2))}`;
    return [...systemMsg, { role: 'system', content: summary }, ...history.slice(-2)];
  }
  return messages;
}
```

### Budget alerts

```typescript
// Check budget thresholds after each LLM call
async function checkBudget(tenantId: string, featureId: string): Promise<void> {
  const monthlyUsage = await getMonthlyCost(tenantId, featureId);
  const budget = await getBudget(tenantId, featureId);

  if (monthlyUsage >= budget * 0.8 && monthlyUsage < budget) {
    await alert(`Tenant ${tenantId} feature ${featureId}: 80% budget used`);
  }
  if (monthlyUsage >= budget) {
    await alert(`Tenant ${tenantId} feature ${featureId}: BUDGET EXCEEDED`, 'critical');
    await throttleFeature(tenantId, featureId);
  }
}
```

### Cost dashboard (SQL query)

```sql
-- Monthly cost by tenant and feature
SELECT
  tenant_id,
  feature_id,
  SUM(cost) AS total_cost,
  SUM(prompt_tokens) AS total_prompt_tokens,
  SUM(completion_tokens) AS total_completion_tokens,
  COUNT(*) AS total_calls
FROM llm_costs
WHERE timestamp >= date_trunc('month', CURRENT_DATE)
GROUP BY tenant_id, feature_id
ORDER BY total_cost DESC;
```

## Preguntas guía

- ¿Cada llamada LLM tiene tags de tenant y feature?
- ¿El modelo más barato que cumple la tarea está siendo usado?
- ¿Hay cache semántico implementado?
- ¿Los prompts tienen contexto innecesario que se puede comprimir?
- ¿Hay alertas de presupuesto por tenant y globales?
- ¿El costo por petición es conocido?

## Salidas esperadas

- Middleware de tracking de tokens con atribución a tenant y feature.
- Configuración de semantic cache con threshold de similitud.
- Tiering matrix con modelos asignados por tipo de tarea.
- Estrategia de prompt compression (system prompt fijo + historial resumido).
- Alertas de presupuesto (80% warning, 100% critical).
- Reporte mensual de costos por tenant, feature y modelo.

## Criterios de calidad

- 100% de llamadas LLM registradas con costo y atribución.
- Cache semántico reduce ≥20% llamadas a modelos grandes.
- Tiering definido: ≤30% de llamas van al tier Premium.
- Alertas configuradas y probadas.
- Costo por petición visible en dashboard de observabilidad.

## Comportamiento esperado del agente

Cuando se detecte una llamada LLM sin tracking, el agente debe instrumentarla con tenant_id y feature_id.
Cuando el mismo prompt se repita sin cache, debe implementar semantic cache.
Cuando una tarea simple use un modelo caro (Sonnet para clasificación trivial), debe proponer downgrade a Haiku/Mini.
Cuando no haya alertas de presupuesto, debe configurarlas con umbrales por tenant.

## Plantilla de respuesta

```
1. Token tracking instrumentation (middleware/decorator).
2. Model tiering matrix (Premium / Standard / Economy).
3. Semantic cache config (threshold, TTL, eviction).
4. Prompt compression strategy.
5. Budget alert rules (per tenant and global).
6. Cost dashboard query (by tenant, feature, model).
```

## Ejemplos

### Ejemplo 1 — Semantic cache savings

```
Before cache: 10k requests/day × 1500 tokens × premium model = $45/day
After cache (35% hit rate): 6.5k LLM calls + 3.5k cache hits = $29.25/day
Savings: 35% ($15.75/day, ~$472/month)
```

### Ejemplo 2 — Model tiering savings

```
Feature: Ticket classification (simple intent detection)
Before: GPT-4o → $0.015/request
After: GPT-4o-mini → $0.002/request
Savings: 87% on that feature
```

## Checklist

- [ ] Token tracking en todas las llamadas LLM (tenant, feature, modelo).
- [ ] Semantic cache con threshold ≥ 0.90 similitud coseno.
- [ ] Tiering matrix documentada y aplicada por feature.
- [ ] Prompt compression implementada para historial largo.
- [ ] Alertas de budget configuradas (80% warning, 100% critical).
- [ ] Reporte mensual de costos por tenant y feature.
- [ ] Costo por petición expuesto en dashboard de observabilidad.
- [ ] Estrategia batch vs streaming definida por caso de uso.
