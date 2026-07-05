# MCP Governance

Este documento define cómo se gobierna el uso de **Model Context Protocol (MCP)** en el framework: qué tipos de servidores están permitidos, quién los autoriza, cómo se clasifican por riesgo, cómo se gestionan los secretos, cómo se audita su uso y cómo se desacoplan por tenant o entorno.

MCP es una capacidad **opcional y gobernada**, no un requisito base del framework. Su uso siempre está sujeto a aprobación y a los controles definidos en este documento.

---

## Principios de gobernanza MCP

1. **Opt-in explícito**: Ningún MCP server se activa por defecto. Cada servidor debe ser declarado, clasificado y aprobado.
2. **Mínimo privilegio**: Los agentes solo acceden a las herramientas MCP que necesitan para su tarea concreta. No hay acceso global.
3. **Auditoría completa**: Todo uso de MCP debe quedar registrado: agente, herramienta, inputs resumidos, outputs resumidos, timestamp.
4. **Aislamiento por tenant**: Un tenant no puede usar MCP servers de otro tenant, ni acceder a datos cruzados vía MCP.
5. **Sin secretos en prompts**: Las credenciales y tokens de MCP servers nunca se pasan como texto en prompts ni en mensajes de agentes.
6. **Reversibilidad**: Cualquier MCP server puede ser desactivado sin afectar la operación core del framework.

---

## Ciclo de vida de un MCP server

```
1. Propuesta → Descripción del servidor, propósito, tipo, riesgo estimado
2. Clasificación → Risk tier (ver tabla) + responsable de autorización
3. Autorización → control-agent + framework-security + framework-governance
4. Integración → framework-core-design (catálogo) + framework-platform (despliegue)
5. Operación → framework-platform (observabilidad) + framework-security (auditoría)
6. Revisión periódica → framework-operations-evolution (SLO, uso, incidentes)
7. Deprecación → framework-governance (registro) + framework-platform (desconexión)
```

---

## Clasificación de MCP servers por tipo y riesgo

| Tipo | Descripción | Risk Tier | Autorización |
|------|-------------|-----------|--------------|
| **Documentación** | Acceso a docs, wikis, specs. Solo lectura. | Low | design-agent puede proponer; control-agent aprueba |
| **Datos internos** | Bases de datos, APIs internas del sistema. Lectura/escritura restringida. | Medium | control-agent + framework-security + framework-governance |
| **Sistemas externos** | APIs de terceros, SaaS, servicios cloud. | Medium-High | control-agent + framework-governance + revisión de SLA |
| **Observabilidad** | Métricas, trazas, logs. Solo lectura de telemetría. | Low-Medium | control-agent aprueba |
| **Automatización** | Ejecución de acciones (deploy, email, notificaciones). | High | framework-governance + usuario final + control-agent |
| **Datos sensibles** | PII, financiero, salud. Requiere cifrado extremo a extremo. | Critical | framework-governance + framework-security + framework-data-memory-compliance + usuario |

---

## Risk Tiers — definición

| Tier | Criterio | Revisión |
|------|----------|----------|
| **Low** | Solo lectura, datos no sensibles, sin impacto en producción | Anual |
| **Medium** | Lectura/escritura de datos no sensibles, o solo lectura de datos internos | Semestral |
| **Medium-High** | Acceso a sistemas externos o escritura de datos internos | Trimestral |
| **High** | Acciones irreversibles, acceso a producción, datos de negocio | Mensual o por cambio |
| **Critical** | Datos regulados, PII, secretos, acceso cross-tenant | Continuo + auditoría obligatoria |

---

## Modelo conceptual de configuración MCP

Cada despliegue del framework que use MCP debe tener un archivo de configuración equivalente al siguiente modelo:

```json
{
  "mcpServers": {
    "<server-id>": {
      "type": "<tipo: documentation|data|external|observability|automation|sensitive-data>",
      "risk_tier": "<low|medium|medium-high|high|critical>",
      "purpose": "<descripción de para qué se usa>",
      "authorized_by": "<control-agent|governance|usuario>",
      "authorized_date": "<fecha>",
      "scope": ["<tenant-id o global>"],
      "allowed_agents": ["<orchestrator-agent|design-agent|control-agent|delivery-agent>"],
      "secrets_managed_by": "<vault|env|secrets-manager>",
      "audit_enabled": true,
      "review_date": "<próxima revisión>",
      "command": "<comando de ejecución si aplica>",
      "args": ["<argumentos>"],
      "env": {
        "<ENV_VAR>": "<referencia al secreto, nunca el valor literal>"
      }
    }
  }
}
```

### Categorías sugeridas para un framework genérico

| ID sugerido | Tipo | Propósito ejemplo |
|-------------|------|-------------------|
| `docs-context` | documentation | Carga de specs, documentación técnica del framework |
| `internal-data` | data | Acceso a bases de datos del sistema agéntico |
| `external-api` | external | Integración con APIs de terceros autorizadas |
| `observability` | observability | Consulta de métricas, trazas y logs |
| `automation-safe` | automation | Notificaciones, webhooks, acciones reversibles |

> No se fijan servidores reales en este documento. La configuración concreta es responsabilidad de cada despliegue, con aprobación según el risk tier.

---

## Gobernanza MCP por capa del framework

### framework-core-design

Responsable de:
- Definir el **catálogo de herramientas MCP** disponibles para los agentes.
- Establecer el contrato de acceso a cada MCP server (inputs, outputs, errores esperados).
- Documentar el modelo de routing de tools entre agentes.

### framework-security

Responsable de:
- Definir **allowlists** de MCP servers por tenant y por agente.
- Establecer **scopes** de acceso (qué operaciones puede hacer cada agente en cada server).
- Diseñar los controles de **auditoría** del uso de MCP (qué se registra, cuánto tiempo, quién lo revisa).
- Definir cómo se gestionan los **secretos** de conexión (rotación, almacenamiento, nunca en prompts).
- Establecer límites de uso por agente y por tenant (rate limits, budget caps).

### framework-platform

Responsable de:
- Definir cómo se **despliegan** los MCP servers (containerizado, sidecar, externo).
- Garantizar la **conectividad segura** entre agentes y MCP servers.
- Implementar la **observabilidad** del uso de MCP (latencia, errores, volumen).
- Gestionar el **aislamiento** por tenant a nivel de red y namespace.
- Operar el ciclo de vida del server en producción.

---

## Gestión de secretos

| Regla | Descripción |
|-------|-------------|
| Nunca en texto plano | Los tokens y credenciales de MCP nunca se almacenan ni transmiten como texto visible |
| Referencia por variable de entorno | El archivo de configuración MCP usa referencias a variables de entorno, no valores directos |
| Rotación programada | Todos los secretos tienen fecha de rotación según el risk tier del servidor |
| Acceso mínimo | Cada agente solo recibe las credenciales de los MCP servers que usa, nada más |
| Auditoría de acceso | Todo acceso a un secreto de MCP queda registrado |

---

## Auditoría del uso de MCP

Para cada invocación de un MCP server, el sistema debe registrar:

| Campo | Descripción |
|-------|-------------|
| `timestamp` | Momento de la invocación |
| `agent_role` | Qué agente invocó el server |
| `tenant_id` | A qué tenant pertenece la sesión |
| `server_id` | Identificador del MCP server |
| `tool_name` | Herramienta específica invocada |
| `input_summary` | Resumen del input (sin datos sensibles) |
| `output_summary` | Resumen del output (sin datos sensibles) |
| `outcome` | `success` / `error` / `rate_limited` |
| `latency_ms` | Tiempo de respuesta |

Los registros de auditoría se retienen según la política de `framework-data-memory-compliance` y el risk tier del servidor.

---

## Desacoplamiento por tenant y entorno

- Cada tenant tiene su propio scope de MCP servers autorizados.
- Un agente operando para el tenant A no puede invocar MCP servers del tenant B.
- Los entornos (development, staging, production) tienen catálogos de MCP separados.
- Los MCP servers de producción no son accesibles desde entornos de desarrollo salvo excepción explícita documentada en `framework-governance`.

---

## Relación con el modelo de agentes

Cada agente tiene definido en `AGENT-MODEL.md` si su `mcp_usage` es `none`, `optional` o `governed`:

| Valor | Significado |
|-------|-------------|
| `none` | El agente no usa MCP servers |
| `optional` | El agente puede usar MCP servers si están autorizados; no es requisito para su función |
| `governed` | El agente usa MCP servers de forma habitual; su uso requiere autorización explícita y auditoría completa |

> El campo `mcp_usage` en el frontmatter de cada `SKILL.md` indica el nivel de uso esperado para esa skill.
