# Framework Agéntico — Índice completo

Este documento es el punto de entrada del framework. Resume qué resuelve cada skill, cuándo usarla, qué artefactos debería producir y cómo se encadenan entre sí.

El framework tiene **58 skills** en dos grupos:
- **12 framework skills** — para diseñar, gobernar y operar el propio framework agéntico.
- **46 stack skills** — para implementar proyectos concretos (APIs, frontends, bases de datos, tests, proceso).

---

## Inicio Rápido

**¿Primera vez usando el framework?**

1. **Instalar y verificar** → Lee [QUICKSTART.md](QUICKSTART.md) (15 min) — Tu primer módulo paso a paso
2. **Workflows comunes** → Consulta [WORKFLOW-GUIDE.md](WORKFLOW-GUIDE.md) — Cómo trabajar módulo por módulo
3. **Preguntas frecuentes** → Revisa [FAQ.md](FAQ.md) — Respuestas rápidas
4. **Solución de problemas** → Ver [TROUBLESHOOTING.md](TROUBLESHOOTING.md) (cuando esté disponible)

**¿Ya conoces el framework?**

- [SKILLS-MANIFEST.md](../framework/SKILLS-MANIFEST.md) — Catálogo completo de 58 skills
- [SKILL-FLOW.md](../framework/SKILL-FLOW.md) — Flujo end-to-end con ejemplo
- [SKILL-EXECUTION-PROTOCOL.md](../framework/SKILL-EXECUTION-PROTOCOL.md) — Protocolo de 7 pasos
- [AGENT-MODEL.md](../framework/AGENT-MODEL.md) — Roles y límites de agentes

**El framework se activa automáticamente** — solo escribe tu petición en lenguaje natural en el chat de VS Code.

---

## Cómo usar este índice

Usa este README para responder tres preguntas rápido:

1. ¿Qué skill necesito ahora?
2. ¿Qué insumos debería tener antes de usarla?
3. ¿Qué salida debería dejar lista para la siguiente fase?

Si necesitas el detalle operativo de una fase, entra directamente a su `SKILL.md`. Si necesitas ver el framework como recorrido completo, usa [SKILL-FLOW.md](../framework/SKILL-FLOW.md). Si vas a crear o normalizar una skill nueva, usa [skills/SKILL-TEMPLATE.md](../skills/SKILL-TEMPLATE.md).

## Inicio rápido — Proyecto agéntico desde cero

**Modo único: Nivel por nivel con confirmación** (25 niveles obligatorios).

El framework ejecuta SIEMPRE el mismo flujo completo. Las skills `framework-*` no son opcionales: cubren multi-tenancy, memoria, guardrails, router de modelos y observabilidad.

```
FASE A — Gobierno y dominio
  1. framework-governance              ✓ Confirmas
  2. framework-discovery               ✓ Confirmas
  3. framework-conception              ✓ Confirmas
  4. framework-pack-design             ✓ Confirmas

FASE B — Arquitectura agéntica
  5. framework-architecture            ✓ Confirmas
  6. framework-core-design             ✓ Confirmas
  7. framework-data-memory-compliance  ✓ Confirmas
  8. framework-security                ✓ Confirmas
  9. framework-platform                ✓ Confirmas

FASE C — Scaffold y proyecto
  10. framework-scaffold-implementation ✓ Confirmas
  11. project-bootstrap                 ✓ Confirmas
  12. repo-structure                    ✓ Confirmas
  13. project-architecture              ✓ Confirmas

FASE D — Especificación
  14. api-first-spec                    ✓ Confirmas

FASE E — Backend
  15. database-sp                       ✓ Confirmas
  16. data-access                       ✓ Confirmas
  17. backend-api                       ✓ Confirmas
  18. swagger                           ✓ Confirmas

FASE F — Frontend
  19. typescript                        ✓ Confirmas
  20. react-hooks                       ✓ Confirmas
  21. react                             ✓ Confirmas

FASE G — Calidad
  22. framework-qa-validation           ✓ Confirmas
  23. playwright                        ✓ Confirmas

FASE H — Operación y release
  24. framework-operations-evolution    ✓ Confirmas
  25. pull-request                      ✓ Fin
```

### Reuso entre módulos del mismo vertical

Los **Niveles 1-13** se ejecutan **una vez por vertical**. Para módulos adicionales del mismo pack se arranca directo en **Nivel 14** y se ejecuta 14→25.

### Meta-skills (atajos opcionales)

Las meta-skills (`agent-backend`, `agent-frontend`, `agent-fullstack`, `agent-qa`) agrupan los niveles 15-23 cuando ya conoces el flujo. Para usarlas, dilo explícitamente: `"usa agent-backend para el módulo X"`. **Nunca reemplazan los Niveles 1-14**.

Ver [EXECUTION-MODES.md](EXECUTION-MODES.md) para detalles.

## Documentación operativa del framework

| Documento | Propósito |
|-----------|-----------|
| [SKILLS-MANIFEST.md](../framework/SKILLS-MANIFEST.md) | Catálogo centralizado: las 58 skills con fase, capa, enforcement, dependencias, agentes y perfiles de validación |
| [SKILL-FLOW.md](../framework/SKILL-FLOW.md) | Recorrido end-to-end del framework con un ejemplo completo de vertical |
| [SKILL-EXECUTION-PROTOCOL.md](../framework/SKILL-EXECUTION-PROTOCOL.md) | Protocolo de 7 pasos para que un agente ejecute una skill correctamente |
| [SKILL-ROUTING.md](../framework/SKILL-ROUTING.md) | Cuándo activar cada skill, cómo combinarlas, inyección cross-cutting y resolución de conflictos de ownership |
| [AGENT-MODEL.md](../framework/AGENT-MODEL.md) | Roles de agentes del framework (orchestrator, design, control, delivery), skills asignadas y límites de autonomía |
| [MCP-GOVERNANCE.md](../framework/MCP-GOVERNANCE.md) | Gobernanza de herramientas MCP: clasificación por riesgo, autorización, secretos, auditoría y aislamiento por tenant |
| [VALIDATION-PROFILES.md](../framework/VALIDATION-PROFILES.md) | Perfiles de validación por skill: criterios, binding skill-perfil y guía para implementación futura de validadores |
| [HOOKS-AND-GUARDRAILS.md](../framework/HOOKS-AND-GUARDRAILS.md) | Diseño de interceptores y guardrails: pre-skill, pre-write, stage completion, MCP check y release gate |
| [skills/SKILL-TEMPLATE.md](../skills/SKILL-TEMPLATE.md) | Plantilla base para crear nuevas skills con metadata extendida |

**Para entrar al framework**: empieza por este README → elige tu skill en la tabla rápida → ejecuta con el protocolo de [SKILL-EXECUTION-PROTOCOL.md](../framework/SKILL-EXECUTION-PROTOCOL.md).

## Flujo recomendado

El orden base de trabajo es este:

1. `framework-governance`
2. `framework-discovery`
3. `framework-conception`
4. `framework-pack-design`
5. `framework-architecture`
6. `framework-core-design`, `framework-data-memory-compliance`, `framework-security` y `framework-platform`
7. `framework-scaffold-implementation`
8. `framework-qa-validation`
9. `framework-operations-evolution`

`framework-architecture` actúa como puente entre la definición funcional y el diseño técnico. A partir de ahí, core, datos, seguridad y plataforma pueden evolucionar en paralelo, pero deben mantenerse alineados con la arquitectura aprobada.

Si necesitas ver este flujo convertido en un caso real, consulta [SKILL-FLOW.md](../framework/SKILL-FLOW.md).

## Tabla rápida

| Skill | Propósito principal | Cuándo usarla | Entrega clave | Siguiente consumidor |
| --- | --- | --- | --- | --- |
| `framework-governance` | Definir reglas rectoras, estándares, excepciones y ownership. | Antes de diseñar el framework o cambiar sus reglas base. | Marco de gobierno y criterios de decisión. | Discovery, architecture, security, operations. |
| `framework-discovery` | Entender problema, actores, procesos, datos e integraciones. | Cuando se explora un vertical o un nuevo pack. | Definición del problema y mapa del dominio. | Conception. |
| `framework-conception` | Convertir el problema en solución funcional. | Cuando ya existe discovery y hace falta definir capacidades, flujos y agentes. | Solución funcional y alcance MVP. | Pack design y architecture. |
| `framework-pack-design` | Diseñar un pack vertical como producto comercializable. | Cuando la oportunidad ya está clara y debe aterrizarse como producto. | Definición del pack, agentes, prompts, runbooks y métricas. | Architecture y scaffold. |
| `framework-architecture` | Mapear la solución completa a capas, contratos y decisiones build vs buy. | Cuando la solución funcional ya existe y debe traducirse a diseño técnico. | Arquitectura integrada y dependencias entre capas. | Core, data, security, platform. |
| `framework-core-design` | Diseñar el núcleo reusable, el SDK y la orquestación agéntica. | Cuando ya está clara la arquitectura y hace falta estabilizar el contrato del core. | Contrato core-pack, SDK y runtime agéntico. | Scaffold y QA. |
| `framework-data-memory-compliance` | Diseñar taxonomía de datos, memorias, stores y controles de compliance. | Cuando hace falta definir capa de datos y memoria con aislamiento por tenant. | Estrategia de datos, memoria, retención y borrado. | Scaffold, security, operations. |
| `framework-security` | Diseñar autorización, guardrails, secretos, auditoría y límites de autonomía. | Cuando la solución debe operar con controles verificables y multi-tenant. | Modelo de acceso, controles y evidencias de auditoría. | Scaffold, platform, operations. |
| `framework-platform` | Diseñar runtime, Kubernetes, mensajería, observabilidad y CI/CD. | Cuando la arquitectura debe volverse operable en entornos reales. | Topología de plataforma y operación mínima viable. | Scaffold, QA, operations. |
| `framework-scaffold-implementation` | Convertir el diseño en repositorios, módulos, plantillas y primer slice funcional. | Cuando el framework ya tiene diseño suficiente para implementarse. | Scaffold inicial y vertical slice. | QA. |
| `framework-qa-validation` | Definir cómo probar contratos, guardrails, integración y estabilidad. | Cuando hace falta validar la base antes de promover cambios o releases. | Estrategia de validación y criterios go/no-go. | Operations y evolución del framework. |
| `framework-operations-evolution` | Definir operación, monitoreo, incidentes, versionado y mejora continua. | Cuando el framework ya debe sostenerse y evolucionar en producción. | Modelo operativo y política de evolución. | Governance y roadmap futuro. |

## Skills disponibles

### 1. Gobierno y descubrimiento

#### `framework-governance`
Establece la constitución del framework: principios obligatorios, estándares por defecto, decisiones variables y manejo de excepciones.

- Archivo: [skills/framework-governance/SKILL.md](../skills/framework-governance/SKILL.md)
- Úsala para definir qué se puede estandarizar, qué debe protegerse como diferenciador y cómo se gobiernan cambios.
- Debe dejar listo un marco de reglas que architecture, security y operations puedan aplicar sin reinterpretarlo cada vez.

#### `framework-discovery`
Delimita el problema de negocio, el contexto operativo, los actores, datos, integraciones y restricciones.

- Archivo: [skills/framework-discovery/SKILL.md](../skills/framework-discovery/SKILL.md)
- Úsala antes de diseñar packs o agentes, para evitar construir sobre supuestos débiles.
- Debe dejar claro el vertical, el proceso objetivo, los datos disponibles y las restricciones clave.

#### `framework-conception`
Transforma el entendimiento del problema en una solución funcional estructurada.

- Archivo: [skills/framework-conception/SKILL.md](../skills/framework-conception/SKILL.md)
- Úsala para pasar de problema detectado a capacidades, agentes, flujos, reglas y alcance MVP.
- Debe dejar una visión funcional que architecture y pack-design puedan aterrizar sin rehacer discovery.

### 2. Diseño del producto y la arquitectura

#### `framework-pack-design`
Diseña cada pack vertical como producto vendible sobre el framework.

- Archivo: [skills/framework-pack-design/SKILL.md](../skills/framework-pack-design/SKILL.md)
- Úsala para delimitar capacidades del pack, agentes especializados, prompts, runbooks, límites y métricas.
- Debe separar claramente conocimiento de dominio del pack frente a responsabilidades del core.

#### `framework-architecture`
Organiza la solución completa en capas, contratos, límites y dependencias técnicas.

- Archivo: [skills/framework-architecture/SKILL.md](../skills/framework-architecture/SKILL.md)
- Úsala para traducir la solución funcional a la arquitectura de 7 capas y tomar decisiones build vs buy.
- Debe dejar reglas claras para core, datos, seguridad, plataforma e implementación inicial.

### 3. Capas transversales del framework

#### `framework-core-design`
Diseña el motor reusable del framework: runtime agéntico, SDK, router model-agnostic y catálogo de herramientas.

- Archivo: [skills/framework-core-design/SKILL.md](../skills/framework-core-design/SKILL.md)
- Úsala cuando la arquitectura ya definió responsabilidades y hace falta estabilizar el núcleo común.
- Debe dejar explícito qué pertenece al core y qué queda en los packs.

#### `framework-data-memory-compliance`
Diseña cómo se clasifican, almacenan, aíslan, retienen y eliminan los datos del sistema.

- Archivo: [skills/framework-data-memory-compliance/SKILL.md](../skills/framework-data-memory-compliance/SKILL.md)
- Úsala para definir memorias, stores, cifrado, retención, borrado y cumplimiento normativo.
- Debe separar con claridad conocimiento global del pack, configuración del tenant, datos del cliente y estado efímero.

#### `framework-security`
Diseña autorización, guardrails, secretos, auditoría y control de acciones críticas.

- Archivo: [skills/framework-security/SKILL.md](../skills/framework-security/SKILL.md)
- Úsala para vender el framework en contextos enterprise o regulados sin dejar seguridad como anexo posterior.
- Debe conectar identidad, políticas, tools, límites de autonomía y facturación granular con evidencia auditable.

#### `framework-platform`
Diseña la plataforma operativa: cómputo, Kubernetes, workflows largos, mensajería, observabilidad y CI/CD.

- Archivo: [skills/framework-platform/SKILL.md](../skills/framework-platform/SKILL.md)
- Úsala para convertir la arquitectura en una plataforma portable, escalable y operable.
- Debe hacer explícita la estrategia de runtime, observabilidad, costos y aislamiento por tenant.

### 4. Implementación, validación y evolución

#### `framework-scaffold-implementation`
Convierte el diseño en una base de código reproducible con repositorios, módulos, plantillas y entorno local.

- Archivo: [skills/framework-scaffold-implementation/SKILL.md](../skills/framework-scaffold-implementation/SKILL.md)
- Úsala cuando core, datos, seguridad y plataforma ya tienen definición suficiente para construir el primer slice.
- Debe dejar un scaffold mínimo pero real, listo para validar y extender.

#### `framework-qa-validation`
Define la estrategia de pruebas y validación por capa del framework.

- Archivo: [skills/framework-qa-validation/SKILL.md](../skills/framework-qa-validation/SKILL.md)
- Úsala para validar contratos, compatibilidad, guardrails, aislamiento por tenant y criterios de aceptación.
- Debe dejar claro qué bloquea un release y qué evidencia se necesita para avanzar.

#### `framework-operations-evolution`
Define cómo se opera, monitorea, corrige y evoluciona el framework una vez existe un primer release.

- Archivo: [skills/framework-operations-evolution/SKILL.md](../skills/framework-operations-evolution/SKILL.md)
- Úsala para establecer monitoreo, incidentes, versionado, compatibilidad, deprecación y mejora continua.
- Debe cerrar el ciclo con feedback hacia governance, roadmap y nuevas iteraciones del framework.

## Reglas de lectura rápida

- Si estás empezando un vertical desde cero, entra por `governance` si faltan reglas y por `discovery` si el problema todavía no está delimitado.
- Si ya sabes qué problema resolver pero no cómo estructurar la solución, usa `conception` y luego `pack-design`.
- Si ya existe una solución funcional y necesitas aterrizarla técnicamente, entra por `architecture`.
- Si ya tienes arquitectura aprobada, diseña `core`, `data-memory-compliance`, `security` y `platform` en paralelo, pero con contratos compartidos.
- Si ya existe diseño técnico suficiente, usa `scaffold-implementation`, luego `qa-validation` y por último `operations-evolution`.

## Relación entre artefactos

La salida de cada skill debería alimentar a la siguiente:

- `governance` define reglas y restricciones que condicionan discovery, architecture, security y operations.
- `discovery` produce el entendimiento del dominio que `conception` transforma en solución funcional.
- `conception` delimita capacidades y flujos que `pack-design` empaqueta como producto y que `architecture` aterriza técnicamente.
- `architecture` reparte responsabilidades entre `core-design`, `data-memory-compliance`, `security` y `platform`.
- Estas cuatro capas dejan contratos e insumos para `scaffold-implementation`.
- `scaffold-implementation` entrega una base verificable para `qa-validation`.
- `qa-validation` y `operations-evolution` cierran el ciclo y devuelven aprendizaje a governance y roadmap.

---

## Stack skills — Tabla de referencia rápida

Skills para implementar proyectos concretos (APIs, webs, móviles). Para el catálogo completo con metadata ver [SKILLS-MANIFEST.md](../framework/SKILLS-MANIFEST.md).

### Proceso y workflow

| Skill | Propósito | Úsala cuando |
|-------|-----------|-------------|
| `project-bootstrap` | Onboarding a un proyecto | Primer día en el proyecto |
| `repo-structure` | Nombrar y tipar repositorios | Crear repo nuevo |
| `project-architecture` | Elegir estilo de arquitectura | Diseño inicial del proyecto |
| `hu-template` | Escribir User Stories | Antes de especificar APIs |
| `html-prototype` | Mockups HTML para stakeholders | Validar pantallas antes de codificar |
| `changelog` | Mantener CHANGELOG.md | Antes de crear PR |
| `code-review` | Checklists de revisión | Antes de abrir PR |
| `pull-request` | Crear PR con conventional commits | Al cerrar una feature |
| `readme` | Documentar módulos | Al crear módulos nuevos |
| `skill-creator` | Crear nuevas skills | Cuando falte un patrón en el framework |
| `skill-sync` | Sincronizar metadata de skills | Después de crear/modificar skills |

### API y especificación

| Skill | Propósito | Úsala cuando |
|-------|-----------|-------------|
| `api-first-spec` | Especificación completa de un módulo | Antes de codificar cualquier cosa |
| `swagger` | Documentar con OpenAPI | Al crear o modificar endpoints |
| `api-first-backend` | Backend desde spec | Generar código desde OpenAPI |
| `api-first-frontend` | Frontend desde spec | Generar types y hooks desde OpenAPI |
| `api-first-testing` | Tests desde spec | Generar tests desde OpenAPI |
| `api-catalog` | Inventario de APIs del sistema | Documentar el sistema completo |

### Base de datos

| Skill | Propósito | Úsala cuando |
|-------|-----------|-------------|
| `database` | Convenciones SQL del proyecto | Definir estándares de DB |
| `database-modeling` | Diseño de tablas y constraints | Crear o modificar tablas |
| `database-sp` | Stored procedures y queries | Crear operaciones CRUD en DB |
| `database-audit` | Auditoría, soft delete, logging | Agregar trazabilidad a tablas |
| `database-security` | Prevención de inyección SQL | Validar inputs en DB |

### Backend

| Skill | Propósito | Úsala cuando |
|-------|-----------|-------------|
| `backend-api` | Estructura de módulo y endpoints | Crear o modificar endpoints |
| `data-access` | Handlers y mapeo de resultados | Implementar acceso a datos |
| `api-integration` | Conexión DB → API | Cablear SP con endpoints |
| `app-bootstrap` | Registro de servicios/middleware | Configurar startup |
| `shared-libs` | ApiResponse, excepciones, identidad | Usar contratos compartidos |
| `error-handling` | Flujo de errores entre capas | Implementar manejo de errores |
| `dotnet-gateway` | API Gateway | Configurar gateway |
| `authentication` | Login, tokens, sesión | Implementar autenticación |
| `authorization` | Permisos, roles, RBAC | Implementar autorización |
| `security` | OWASP, CORS, headers | Aplicar controles de seguridad |
| `performance` | Paginación, caché, optimización | Optimizar queries y respuestas |
| `docker-local` | Docker local dev | Configurar contenedores |
| `notifications` | Email, push, in-app, webhooks | Implementar notificaciones |
| `export-excel` | Exportación a Excel | Implementar descarga de datos |

### Frontend

| Skill | Propósito | Úsala cuando |
|-------|-----------|-------------|
| `typescript` | TypeScript strict patterns | Escribir tipos e interfaces |
| `design-system` | Tokens, tipografía, componentes | Aplicar diseño consistente |
| `react` | Feature folders, routing, pages | Crear features React/Angular/Vue |
| `react-hooks` | Query/Mutation/Logic hooks | Implementar comunicación con API |
| `microfrontend` | Module Federation | Configurar microfrontends |

### Testing

| Skill | Propósito | Úsala cuando |
|-------|-----------|-------------|
| `playwright` | Tests E2E con Playwright | Escribir o configurar tests E2E |

### Meta-skills (orquestación)

| Skill | Activa | Úsala cuando |
|-------|--------|-------------|
| `agent-backend` | Todas las skills backend en secuencia | Feature backend completo |
| `agent-frontend` | Todas las skills frontend en secuencia | Feature frontend completo |
| `agent-fullstack` | Backend + frontend + QA | Feature full-stack completo |
| `agent-qa` | Skills de testing en secuencia | Plan de QA completo |

## Criterio de calidad del framework documental

Una skill está bien definida si permite responder, sin ambigüedad:

1. Cuándo se activa.
2. Qué insumos necesita.
3. Qué decisiones le pertenecen.
4. Qué artefactos debe producir.
5. Qué skill consume su salida.

Si una fase no puede responder esas cinco preguntas, todavía necesita refinarse.

La [skills/SKILL-TEMPLATE.md](../skills/SKILL-TEMPLATE.md) ya sigue este criterio y puede usarse como baseline para futuras incorporaciones.

Si necesitas verificar que un usuario nuevo puede orientarse con el sistema completo, la pregunta de control es: ¿puede responder qué skills existen, qué metadata tienen, cómo se resuelven, qué agentes las usan, cómo se gobierna MCP, qué validación futura existe y qué hooks podrían aplicarse? Si no, algún documento está incompleto.