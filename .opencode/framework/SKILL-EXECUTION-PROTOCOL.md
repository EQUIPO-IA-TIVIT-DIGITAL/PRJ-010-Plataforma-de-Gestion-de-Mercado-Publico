# Skill Execution Protocol

Este documento define **cómo un agente debe ejecutar una skill** del framework: desde el contexto de entrada hasta el cierre de la fase con handoff validado.

El protocolo es obligatorio para cualquier agente que opere sobre el framework. Se complementa con `SKILL-ROUTING.md` (cuándo activar cada skill) y `SKILLS-MANIFEST.md` (catálogo y metadata).

---

## Principios del protocolo

1. **Carga explícita primero**: Un agente no puede producir artefactos de una fase sin haber cargado y leído el `SKILL.md` correspondiente.
2. **Dependencias antes que ejecución**: Si la skill tiene `depends_on`, esas skills deben haber producido sus artefactos antes de comenzar esta.
3. **Decisiones abiertas documentadas**: Toda decisión que no se pueda cerrar en la ejecución actual debe registrarse explícitamente como decision abierta.
4. **Handoff declarado**: Al cerrar una fase, el agente debe declarar qué artefactos entrega y a qué skill/agente van dirigidos.
5. **Trazabilidad**: Cualquier decisión relevante debe quedar registrada, no solo el resultado.

---

## Ciclo de ejecución de una skill

### Paso 1 — Resolución de contexto

Antes de comenzar, el agente debe responder:

| Pregunta | Fuente |
|----------|--------|
| ¿Qué skill estoy ejecutando? | `SKILLS-MANIFEST.md` o instrucción del orquestador |
| ¿En qué fase del proyecto estoy? | Contexto del proyecto, `SKILL-FLOW.md` |
| ¿Qué artefactos de entrada existen ya? | Estado del workspace, artefactos de skills previas |
| ¿Hay restricciones de governance activas? | `framework-governance` siempre en contexto |

**Condición de bloqueo**: Si no hay suficiente contexto de entrada para operar, el agente debe escalar al usuario o al orchestrator-agent antes de producir artefactos.

---

### Paso 2 — Verificación de prerequisitos

```
1. Cargar SKILL.md de la skill activa.
2. Leer campo `depends_on` del frontmatter.
3. Para cada dependencia:
   a. Verificar que la skill dependiente ha sido ejecutada.
   b. Verificar que sus artefactos de salida están disponibles.
   c. Si faltan artefactos: registrar como bloqueante y escalar.
4. Verificar enforcement level:
   - mandatory: ejecutar obligatoriamente, documentar excepciones.
   - recommended: documentar si se omite.
   - optional: ejecutar si hay contexto suficiente.
```

---

### Paso 3 — Inyección de skills cross-cutting

Antes de ejecutar la skill principal, evaluar si aplican skills cross-cutting adicionales:

```
if proyecto tiene multi-tenancy:
    cargar framework-security + framework-data-memory-compliance

if proyecto tiene datos sensibles o regulados:
    cargar framework-data-memory-compliance + framework-security

if hay MCP servers nuevos:
    cargar framework-core-design + framework-security + framework-platform

if hay cambios estructurales al framework:
    revalidar framework-governance
```

> Ver tabla completa de condicionales en `SKILLS-MANIFEST.md`.

---

### Paso 4 — Ejecución de la skill

El agente ejecuta la skill siguiendo las instrucciones de su `SKILL.md`. Durante la ejecución:

- **Producir artefactos** según la sección "Salidas esperadas" de la skill.
- **Aplicar reglas críticas** listadas en la skill (SIEMPRE / NUNCA).
- **Consultar decisiones** de la skill: qué decide esta skill vs. qué delega a otras.
- **Registrar decisiones tomadas**: Para cada decisión relevante, documentar la opción elegida y la justificación.

---

### Paso 5 — Registro de decisiones abiertas

Al terminar la ejecución, identificar y documentar decisiones que no se pudieron cerrar:

```markdown
## Decisiones abiertas

| ID | Decisión pendiente | Motivo del bloqueo | Skill/Agente responsable de resolver |
|----|-------------------|--------------------|--------------------------------------|
| DA-001 | ... | ... | ... |
```

**Regla**: Una decisión abierta no bloquea el cierre de la fase si está documentada y el consumidor puede trabajar con ella como assumption. Si bloquea directamente al consumidor, escalar al orchestrator-agent.

---

### Paso 6 — Validación de handoff

Antes de declarar la fase cerrada, verificar:

```
☐ Todos los artefactos declarados en "Salidas esperadas" de la skill están producidos.
☐ Las decisiones abiertas están documentadas con responsable asignado.
☐ El campo `consumed_by` del frontmatter indica a quién se entregan los artefactos.
☐ No hay reglas SIEMPRE/NUNCA violadas sin excepción registrada.
☐ Si la skill tiene `validation_profile`, los criterios correspondientes pueden aplicarse (ver VALIDATION-PROFILES.md).
```

#### Pausa obligatoria para confirmación del usuario

Después de validar el handoff y antes de proceder al Paso 7, el agente DEBE:

1. **Mostrar resumen de completitud** en el formato estandarizado:
   ```
   [Nivel X: nombre-skill] - COMPLETADO
   
   Resumen:
   - Artefacto 1: [descripción]
   - Artefacto 2: [descripción]
   - Decisión tomada: [descripción]
   
   ¿Deseas continuar con [Nivel X+1: siguiente-skill]?
   ```

2. **ESPERAR respuesta explícita** del usuario antes de continuar a otra skill.

3. **NO asumir que el silencio es aprobación** - el usuario debe responder activamente.

4. **NO continuar automáticamente** aunque todo haya salido bien y no haya errores.

5. **Sugerir meta-skills solo al final**: Si esta es la última skill de un flujo completo, entonces y solo entonces, sugerir:
   ```
   Todas las skills individuales completadas.
   ¿Quieres que active [agent-backend/agent-frontend/agent-fullstack] para automatizar futuros módulos?
   ```

**Excepciones a la pausa**:
- Skills de utilidad interna como `skill-sync` o `changelog` que no producen artefactos de negocio principales.
- Cuando el usuario ha invocado explícitamente una meta-skill (`agent-backend`, `agent-frontend`, `agent-fullstack`, `agent-qa`).

---

### Paso 7 — Declaración de cierre

El agente declara el cierre de la fase con el siguiente formato mínimo:

```markdown
## Cierre de fase: [nombre-skill]

**Estado**: Completo / Completo con decisiones abiertas / Bloqueado

**Artefactos entregados**:
- [artefacto 1]: [descripción breve]
- [artefacto 2]: [descripción breve]

**Consumidores**:
- [skill o agente receptor]: [qué recibe]

**Decisiones abiertas**: [ninguna / ver sección DA-XXX]

**Excepciones de governance**: [ninguna / descripción]
```

---

## Manejo de bloqueantes

| Tipo de bloqueante | Acción |
|-------------------|--------|
| Prerequisito no disponible | Escalar al orchestrator-agent con descripción del artefacto faltante |
| Contexto de entrada insuficiente | Escalar al usuario con preguntas específicas |
| Conflicto con framework-governance | Registrar excepción, escalar a control-agent |
| Skill dependiente no ejecutada | No avanzar; solicitar ejecución previa |
| Decisión que excede el scope de la skill | Delegar a la skill correspondiente según `SKILLS-MANIFEST.md` |

---

## Interacción con validadores

Cada skill tiene un `validation_profile` en su frontmatter. El protocolo de ejecución no activa validadores automáticamente en esta iteración, pero el agente debe:

1. Conocer qué perfil de validación aplica a la skill (ver frontmatter).
2. Producir artefactos coherentes con los criterios de ese perfil.
3. Declarar en el cierre si los criterios del perfil se pueden satisfacer o hay gaps.

> Definición completa de perfiles en `VALIDATION-PROFILES.md`.

---

## Interacción entre agentes durante la ejecución

Cuando una skill requiere múltiples agentes (campo `agent_roles`):

- El **agente primario** (primero en la lista) lidera la ejecución y produce los artefactos.
- Los **agentes secundarios** pueden ser consultados para revisión, validación o decisiones que caen en su dominio.
- El **orchestrator-agent** coordina cuándo escalar entre agentes.

> Definición de roles y responsabilidades en `AGENT-MODEL.md`.

---

## Referencia rápida

```
[Inicio de fase]
    │
    ▼
1. Resolver contexto (¿qué skill? ¿qué entradas? ¿governance?)
    │
    ▼
2. Verificar prerequisitos (depends_on → artefactos disponibles)
    │
    ▼
3. Inyectar skills cross-cutting (multi-tenancy, datos sensibles, MCP)
    │
    ▼
4. Ejecutar skill (SKILL.md: reglas, decisiones, artefactos)
    │
    ▼
5. Registrar decisiones abiertas
    │
    ▼
6. Validar handoff (checklist de cierre)
    │
    ▼
7. Declarar cierre → entregar a consumed_by
```
