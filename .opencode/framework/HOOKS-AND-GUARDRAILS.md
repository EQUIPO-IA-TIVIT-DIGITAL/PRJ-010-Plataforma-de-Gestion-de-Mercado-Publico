# Hooks and Guardrails

Este documento describe los **interceptores y controles de uso** del framework: qué verificaciones podrían aplicarse automáticamente en distintos puntos del ciclo de ejecución, cómo funcionarían y cuándo se activarían.

Este es un **diseño documental**, no una implementación activa. Los hooks y guardrails descritos aquí definen el contrato para una futura capa de enforcement automático, sin forzar esa implementación en esta iteración.

Se complementa con `VALIDATION-PROFILES.md` (criterios de validación por skill), `SKILL-EXECUTION-PROTOCOL.md` (cómo ejecuta un agente una skill) y `MCP-GOVERNANCE.md` (controles de herramientas externas).

---

## Tipos de hooks

### 1. Pre-skill hook

**Cuándo se activa**: Antes de que un agente comience la ejecución de una skill.

**Propósito**: Verificar que las condiciones están listas para comenzar sin errores ni violaciones de contrato.

**Verificaciones**:
- ¿El `SKILL.md` de la skill a ejecutar ha sido cargado?
- ¿Los artefactos de las skills en `depends_on` están disponibles?
- ¿La skill tiene `enforcement: mandatory` y hay alguna razón para no ejecutarla? (→ forzar documentación de excepción)
- ¿Hay skills cross-cutting que deberían inyectarse según el contexto del proyecto?
- ¿La skill es coherente con la fase actual del proyecto?

**Acción si falla**:
- Bloquear la ejecución de la skill.
- Registrar el motivo del bloqueo.
- Escalar al orchestrator-agent.

**Ejemplo de regla**:
```
IF skill.depends_on contains "framework-architecture"
AND artefacto("framework-architecture") NOT EXISTS
THEN BLOCK + "Prerequisito no disponible: framework-architecture no ha producido artefactos"
```

---

### 2. Pre-write hook

**Cuándo se activa**: Antes de que un agente escriba o modifique un artefacto.

**Propósito**: Garantizar que el agente tiene autorización para escribir ese artefacto y que la skill correcta está en contexto.

**Verificaciones**:
- ¿El artefacto a crear/modificar corresponde al scope de la skill activa?
- ¿El agente que escribe tiene el rol correcto para esa skill (según `AGENT-MODEL.md`)?
- ¿El artefacto no va a sobrescribir un output de otra skill sin handoff explícito?
- ¿El contenido no contiene secretos o datos sensibles en texto plano?

**Acción si falla**:
- Alertar al agente activo.
- Registrar el intento y el motivo del bloqueo.
- Si es crítico (secreto en texto plano, cross-tenant): bloquear hard.

---

### 3. Stage completion check

**Cuándo se activa**: Cuando un agente declara el cierre de una fase (skill completada).

**Propósito**: Verificar que el handoff es válido antes de pasar artefactos al consumidor.

**Verificaciones**:
- ¿Todos los artefactos declarados en "Salidas esperadas" de la skill existen?
- ¿Las decisiones abiertas están documentadas con responsable?
- ¿El `validation_profile` de la skill puede satisfacerse con los artefactos producidos?
- ¿Las reglas SIEMPRE/NUNCA de la skill se han respetado o las excepciones están documentadas?

**Acción si falla**:
- No permitir el handoff al consumidor.
- Listar los items faltantes.
- El agente debe completarlos o documentar la excepción antes de cerrar.

---

### 4. MCP usage check

**Cuándo se activa**: Antes de que un agente invoque cualquier herramienta de un MCP server.

**Propósito**: Garantizar que el uso de MCP respeta la gobernanza definida en `MCP-GOVERNANCE.md`.

**Verificaciones**:
- ¿El MCP server está en el catálogo autorizado?
- ¿El agente tiene permiso para usar este server (campo `allowed_agents` en la config MCP)?
- ¿El tenant activo tiene acceso a este server (scope por tenant)?
- ¿El rate limit del servidor no está excedido?
- ¿El budget de uso no está agotado?
- ¿La operación a realizar (tool_name) está dentro de las operaciones permitidas?

**Acción si falla**:
- Bloquear la invocación.
- Registrar en el log de auditoría: agente, server, tool, motivo del bloqueo.
- Si el server no está en el catálogo: escalar a control-agent para revisión.

---

### 5. Validation gate (release gate)

**Cuándo se activa**: Como gate antes de avanzar de la fase de implementación a operación.

**Propósito**: Verificar el release-gate completo antes de declarar el sistema listo para producción.

**Verificaciones**: Todos los criterios del perfil `release-gate` en `VALIDATION-PROFILES.md`.

**Acción si falla**:
- Declarar el gate como no superado.
- Listar los criterios fallidos con evidencia.
- El sistema no puede avanzar a producción hasta que el gate esté verde.
- Registrar el resultado del gate en el artefacto de `framework-qa-validation`.

---

### 6. Governance change check

**Cuándo se activa**: Cuando se propone un cambio estructural al framework (nuevo pack, cambio de blueprint, nueva skill mandatory).

**Propósito**: Garantizar que los cambios estructurales pasan por `framework-governance` antes de aplicarse.

**Verificaciones**:
- ¿El cambio propuesto fue revisado con `framework-governance`?
- ¿Hay excepciones al blueprint? ¿Están aprobadas y documentadas?
- ¿El impacto en packs existentes ha sido evaluado?
- ¿El cambio tiene owner y fecha de revisión?

**Acción si falla**:
- Bloquear el cambio hasta que framework-governance lo haya procesado.
- Registrar el cambio pendiente.

---

## Guardrails de agentes

Los guardrails son restricciones permanentes que aplican a todos los agentes, independientemente de la skill activa:

| Guardrail | Descripción | Enforcement |
|-----------|-------------|-------------|
| No secretos en texto plano | Ningún agente puede escribir tokens, passwords o claves en artefactos o prompts | Hard block |
| No cross-tenant data | Un agente operando para tenant A no puede acceder a datos de tenant B | Hard block |
| No MCP fuera de catálogo | Ningún agente puede invocar un MCP server no registrado | Hard block |
| No modificar artefactos de otras fases sin handoff | Un agente no puede sobrescribir el output de otra skill sin declarar el handoff | Soft block + alerta |
| No omitir skills mandatory sin excepción | Saltar una skill mandatory requiere excepción documentada | Soft block + alerta |
| No producir artefactos sin decisiones registradas | Todo artefacto debe tener al menos un registro de decisiones tomadas | Alerta |
| No acciones irreversibles sin confirmación | Deploy a producción, borrado de datos: requieren confirmación explícita | Hard block hasta confirmación |

**Hard block**: La acción se bloquea completamente hasta que el problema se resuelva.  
**Soft block + alerta**: La acción puede continuar pero se registra una alerta y se requiere documentación de excepción.  
**Alerta**: Se notifica pero no bloquea.

---

## Guardrails específicos de MCP

| Guardrail | Descripción |
|-----------|-------------|
| Mínimo privilegio | Un agente solo recibe acceso a los MCP servers que su rol necesita, no a todos |
| Rate limiting | Cada MCP server tiene un límite de invocaciones por agente por sesión |
| Budget cap | El costo acumulado de uso de MCP servers tiene un techo configurable por tenant |
| Auditoría completa | Toda invocación a MCP queda registrada (ver `MCP-GOVERNANCE.md`) |
| Sin datos sensibles en inputs MCP | Los inputs a MCP servers no pueden contener PII sin cifrado previo |

---

## Prioridad de resolución de hooks

Cuando múltiples hooks se activan simultáneamente:

```
1. Hard block (secretos, cross-tenant, MCP no autorizado) — SIEMPRE primero
2. Stage completion check — antes de cualquier handoff
3. Pre-skill hook — antes de comenzar una skill
4. MCP usage check — antes de cada invocación MCP
5. Pre-write hook — antes de escribir cada artefacto
6. Governance change check — antes de aplicar cambios estructurales
7. Validation gate — como gate final
```

---

## Hoja de ruta de implementación

Esta tabla muestra el orden recomendado para implementar los hooks si se decide activarlos en una segunda iteración técnica:

| Hook | Prioridad | Complejidad | Impacto |
|------|-----------|-------------|---------|
| Pre-write (secretos en texto plano) | Alta | Baja | Crítico — previene fugas de secretos |
| MCP usage check | Alta | Media | Crítico — gobierna herramientas externas |
| Pre-skill hook (depends_on) | Alta | Baja | Alto — evita ejecuciones sin prerequisitos |
| Validation gate (release-gate) | Alta | Alta | Alto — controla avance a producción |
| Stage completion check | Media | Media | Medio — garantiza handoffs limpios |
| Governance change check | Media | Media | Medio — protege la integridad del blueprint |
| Pre-write (scope de agente) | Baja | Alta | Bajo — refinamiento fino de permisos |

---

## Alcance de esta iteración

**Incluido**: Diseño de todos los tipos de hooks, criterios, acciones y prioridades. Guardrails de agentes y MCP definidos como contrato.  
**Excluido**: Implementación de hooks como código ejecutable, integración con el runtime de agentes, CI/CD enforcement, hooks activos en producción.
