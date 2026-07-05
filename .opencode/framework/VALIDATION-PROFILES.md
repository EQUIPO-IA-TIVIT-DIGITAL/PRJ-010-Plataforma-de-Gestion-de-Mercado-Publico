# Validation Profiles

Este documento define los **perfiles de validación** del framework: qué tipo de verificación corresponde a cada skill, qué criterios incluye cada perfil y cómo se bindea una skill a su perfil.

Los perfiles son un contrato documental para **futura implementación de validadores deterministas**. En esta iteración, sirven para:
1. Que cada skill sepa qué tipo de validación le corresponde.
2. Que los agentes produzcan artefactos coherentes con esos criterios.
3. Que la implementación de validadores futuros tenga un contrato claro desde el que partir.

No se implementa ningún runner automático en esta versión. El diseño está listo para una segunda iteración técnica.

---

## Perfiles disponibles

### `documentation`

**Propósito**: Verificar que los artefactos de salida de una skill están completos, estructurados y navegables.

**Criterios**:
- [ ] El artefacto existe y tiene contenido no vacío.
- [ ] Tiene todas las secciones obligatorias definidas en la skill (Propósito, Reglas críticas, Salidas esperadas).
- [ ] Las secciones obligatorias no contienen placeholders sin completar (ej. `[pendiente]`, `TODO`).
- [ ] El artefacto es navegable: tiene encabezados, listas o tablas que permiten lectura rápida.
- [ ] Si la skill tiene dependencias (`depends_on`), el artefacto referencia los artefactos de las skills predecesoras.

**Skills que usan este perfil**:
- `framework-discovery`
- `framework-conception` (combinado con `skill-contract`)
- `framework-scaffold-implementation` (combinado con `skill-contract`)
- `framework-operations-evolution`

---

### `skill-contract`

**Propósito**: Verificar que los artefactos respetan el contrato de la skill: inputs declarados, outputs producidos, decisiones registradas, handoff declarado.

**Criterios**:
- [ ] Todas las entradas mínimas de la skill están disponibles antes de la ejecución.
- [ ] Todos los artefactos declarados en "Salidas esperadas" de la skill han sido producidos.
- [ ] Las decisiones tomadas durante la ejecución están documentadas.
- [ ] Las decisiones abiertas están registradas con responsable asignado.
- [ ] El handoff está declarado: qué se entrega y a qué skill/agente.
- [ ] No se han tomado decisiones fuera del scope de la skill sin delegación explícita.

**Skills que usan este perfil**:
- `framework-conception` (combinado con `documentation`)
- `framework-core-design` (combinado con `architecture-consistency`)
- `framework-pack-design`
- `framework-scaffold-implementation` (combinado con `documentation`)

---

### `architecture-consistency`

**Propósito**: Verificar que las decisiones de diseño son internamente consistentes y respetan el blueprint del framework.

**Criterios**:
- [ ] El diseño propuesto puede mapearse a las 7 capas del framework.
- [ ] No hay componentes sin capa asignada.
- [ ] Las decisiones Build vs Buy están justificadas.
- [ ] El routing de modelos está definido (si aplica).
- [ ] El diseño respeta las reglas del blueprint definidas en `framework-governance`.
- [ ] Si hay multi-tenancy, el aislamiento está explícito en el diseño.
- [ ] Las interfaces entre capas (contratos) están definidas, no implícitas.
- [ ] El diseño de core y el de pack no se solapan (ownership claro en `framework-core-design`).

**Skills que usan este perfil**:
- `framework-architecture`
- `framework-core-design` (combinado con `skill-contract`)
- `framework-platform`

---

### `security-review`

**Propósito**: Verificar que los controles de seguridad están diseñados y que no hay vectores de ataque evidentes en los artefactos de diseño.

**Criterios**:
- [ ] RBAC definido: roles, permisos y herencia están documentados.
- [ ] Guardrails declarados: qué restricciones aplican a los agentes y herramientas.
- [ ] Gestión de secretos definida: sin secretos en texto plano en artefactos.
- [ ] Auditoría diseñada: qué eventos se registran, quién los puede leer.
- [ ] Límites de autonomía de agentes documentados.
- [ ] Si hay MCP servers: clasificados por risk tier y con autorización registrada.
- [ ] Si hay datos sensibles: cifrado en tránsito y en reposo definido.
- [ ] No hay violaciones evidentes del OWASP Top 10 en el diseño.

**Skills que usan este perfil**:
- `framework-security`
- `framework-data-memory-compliance` (combinado con `tenant-isolation`)

---

### `tenant-isolation`

**Propósito**: Verificar que el diseño garantiza aislamiento real entre tenants y que no hay rutas de fuga de datos entre ellos.

**Criterios**:
- [ ] El modelo de datos tiene campo `tenant_id` o equivalente en todas las entidades multi-tenant.
- [ ] No hay queries cross-tenant sin control explícito.
- [ ] La eliminación de un tenant tiene un proceso definido (cascada, retención, borrado auditado).
- [ ] Los namespaces o contextos de agentes están aislados por tenant.
- [ ] Los MCP servers tienen scope por tenant (no globales sin autorización).
- [ ] Los logs y trazas tienen tenant_id para filtrado.
- [ ] La memoria de agentes no persiste datos de tenant A en sesiones de tenant B.

**Skills que usan este perfil**:
- `framework-data-memory-compliance` (combinado con `security-review`)

---

### `release-gate`

**Propósito**: Verificar que el sistema está listo para avanzar a la siguiente fase (típicamente de implementación a operación), según los criterios de aceptación del proyecto.

**Criterios**:
- [ ] Todos los artefactos obligatorios de las fases anteriores existen y están completos.
- [ ] Los criterios de aceptación del MVP definidos en conception están verificados.
- [ ] Las pruebas de contrato entre capas (API, SDK) pasan.
- [ ] Las pruebas de integración críticas pasan.
- [ ] No hay decisiones abiertas bloqueantes sin resolución.
- [ ] Los guardrails de seguridad están activos en el entorno de destino.
- [ ] El aislamiento multi-tenant está verificado en el entorno de destino.
- [ ] Los SLOs están definidos y el sistema de monitoreo está operativo.
- [ ] El runbook de operaciones existe y ha sido revisado.

**Skills que usan este perfil**:
- `framework-qa-validation`

---

### `governance-review`

**Propósito**: Verificar que las decisiones tomadas respetan el blueprint del framework y que las excepciones están correctamente registradas.

**Criterios**:
- [ ] Las reglas obligatorias del framework no han sido violadas.
- [ ] Las reglas estándar se han aplicado salvo excepción documentada.
- [ ] Las excepciones tienen justificación, owner y fecha de revisión.
- [ ] Las decisiones variables están dentro del rango permitido por el blueprint.
- [ ] No hay capacidades o componentes fuera del blueprint sin excepción aprobada.
- [ ] Si hay un nuevo pack vertical: se ha verificado que no viola ninguna regla mandatory.

**Skills que usan este perfil**:
- `framework-governance`

---

## Binding skill → perfil de validación

Esta tabla replica lo definido en `SKILLS-MANIFEST.md` como referencia directa:

| Skill | Perfil(es) | Validador futuro sugerido |
|-------|------------|--------------------------|
| `framework-governance` | `governance-review` | governance-checker |
| `framework-discovery` | `documentation` | doc-completeness |
| `framework-conception` | `documentation`, `skill-contract` | doc-completeness + conception-checker |
| `framework-architecture` | `architecture-consistency` | arch-consistency-checker |
| `framework-core-design` | `architecture-consistency`, `skill-contract` | core-design-checker |
| `framework-pack-design` | `skill-contract` | pack-contract-checker |
| `framework-data-memory-compliance` | `tenant-isolation`, `security-review` | compliance-checker |
| `framework-security` | `security-review` | security-checker |
| `framework-platform` | `architecture-consistency` | platform-checker |
| `framework-scaffold-implementation` | `skill-contract`, `documentation` | scaffold-checker |
| `framework-qa-validation` | `release-gate` | qa-gate-checker |
| `framework-operations-evolution` | `documentation` | ops-checker |

---

## Cómo implementar un validador futuro

Cuando se quiera materializar un validador para un perfil:

1. **Leer los criterios del perfil** en este documento.
2. **Mapear cada criterio a una verificación determinista**: regex, existencia de archivo, lint, schema check, test.
3. **Crear el validador** como script independiente (Python, shell, etc.) que recibe el artefacto como input y retorna pass/fail con evidencia.
4. **Registrar en `SKILLS-MANIFEST.md`** el campo `validates_with` con el identificador del validador.
5. **Activar el validador** en el hook correspondiente (ver `HOOKS-AND-GUARDRAILS.md`).

---

## Alcance de esta iteración

**Incluido**: Diseño documental de perfiles y criterios, binding skill-perfil, guía para implementación futura.  
**Excluido**: Runners ejecutables, hooks activos, integración con CI/CD, enforcement automático.

La siguiente iteración técnica puede tomar estos perfiles como contrato base sin necesidad de rediseño.
