# Modos de Ejecución del Framework

Este documento explica cómo se ejecuta el flujo único de 25 niveles del Framework Agéntico y los modos disponibles.

---

## Flujo único obligatorio (25 niveles)

El framework ejecuta SIEMPRE el mismo recorrido completo. Las skills `framework-*` NO son opcionales: definen multi-tenancy, memoria, guardrails, router de modelos y observabilidad. Sin ellas se construye una app CRUD convencional, no una app de agentes.

```
FASE A — GOBIERNO Y DOMINIO
  Nivel 1:  framework-governance
  Nivel 2:  framework-discovery
  Nivel 3:  framework-conception
  Nivel 4:  framework-pack-design

FASE B — ARQUITECTURA AGÉNTICA
  Nivel 5:  framework-architecture
  Nivel 6:  framework-core-design
  Nivel 7:  framework-data-memory-compliance
  Nivel 8:  framework-security
  Nivel 9:  framework-platform

FASE C — SCAFFOLD Y PROYECTO
  Nivel 10: framework-scaffold-implementation
  Nivel 11: project-bootstrap
  Nivel 12: repo-structure
  Nivel 13: project-architecture

FASE D — ESPECIFICACIÓN
  Nivel 14: api-first-spec

FASE E — BACKEND
  Nivel 15: database-sp
  Nivel 16: data-access
  Nivel 17: backend-api
  Nivel 18: swagger

FASE F — FRONTEND
  Nivel 19: typescript
  Nivel 20: react-hooks
  Nivel 21: react

FASE G — CALIDAD
  Nivel 22: framework-qa-validation
  Nivel 23: playwright

FASE H — OPERACIÓN Y RELEASE
  Nivel 24: framework-operations-evolution
  Nivel 25: pull-request
```

### Reuso entre módulos del mismo vertical

Los **Niveles 1-13** se ejecutan **una vez por vertical**. Cuando se agrega un módulo nuevo dentro del mismo pack, se arranca en **Nivel 14** (api-first-spec) y se ejecuta 14→25.

---

## Modo 1: Nivel por Nivel (Por Defecto)

### Características
- Ejecuta UNA skill a la vez
- Espera confirmación explícita del usuario
- Muestra resumen después de cada skill
- Permite revisar resultados antes de continuar
- Recomendado para: Primeras implementaciones, aprendizaje, proyectos críticos

### Ejemplo de uso
```
Usuario: "Quiero crear un proyecto agéntico para un banco"

Asistente: [Nivel 1: framework-governance]
           ... ejecuta ...
           
           [COMPLETADO]
           Resumen: Constitución del proyecto definida
           ¿Continuar con [Nivel 2: framework-discovery]?

Usuario: "Sí" → Continúa
Usuario: "No" → Se detiene
Usuario: "Modifica X primero" → Ajusta antes de continuar
```

### Cómo invocarlo
Por defecto siempre se usa este modo. Basta con:
```
"Crea un proyecto agéntico para [vertical]"
"Quiero construir un pack de [dominio]"
```

---

## Modo 2: Meta-Skills (Atajos opcionales)

### Características
- Agrupan varios niveles consecutivos de implementación en un solo paso
- NO esperan confirmación entre sub-pasos internos
- Cubren solo los Niveles 15–23 (implementación y QA técnica)
- **NUNCA reemplazan los Niveles 1–14** (governance, arquitectura, spec)
- Recomendado para: Módulos adicionales sobre un vertical ya diseñado

### Meta-skills disponibles

#### agent-backend
Agrupa Niveles 15–18: `database-sp` → `data-access` → `backend-api` → `swagger`

#### agent-frontend
Agrupa Niveles 19–21: `typescript` → `react-hooks` → `react`

#### agent-fullstack
Agrupa Niveles 14–23: `api-first-spec` → `agent-backend` → `agent-frontend` → `playwright`

#### agent-qa
Agrupa Niveles 22–23: `framework-qa-validation` → `playwright`

### Ejemplo de uso
```
Usuario: "Usa agent-backend para implementar Embarcaciones"

Asistente: [agent-backend] Iniciando flujo completo...
           [database-sp] Creando tablas...
           [data-access] Creando handlers...
           [backend-api] Creando endpoints...
           [swagger] Generando docs...
           
           [COMPLETADO] Backend completo listo
```

### Cómo invocarlo
```
"Usa agent-backend para X"
"Activa agent-fullstack para Y"
"Hazlo todo de golpe con agent-backend"
```

---

## Comparación

| Aspecto | Nivel por Nivel | Meta-Skills |
|---------|----------------|-------------|
| Control | Alto | Bajo |
| Velocidad | Lento | Rápido |
| Revisión | Entre cada skill | Solo al final |
| Aprendizaje | Alto | Bajo |
| Riesgo de error acumulado | Bajo | Medio |
| Uso recomendado | Primera vez, crítico | Repetitivo, simple |

---

## Cambiar de modo

### De Nivel por Nivel → Meta-Skill
```
Usuario: "Ya entendí el flujo, de ahora en adelante usa agent-backend"
```

### De Meta-Skill → Nivel por Nivel
```
Usuario: "Detente, continúa nivel por nivel con confirmación"
```

---

## Preguntas Frecuentes

**¿Puedo mezclar ambos modos?**
Sí. Ejemplo:
```
1. api-first-spec (manual)
2. agent-backend (automático para backend completo)
3. react + react-hooks (manual para UI paso a paso)
```

**¿Cómo detengo una meta-skill en progreso?**
```
Usuario: "Detente después de data-access"
```

**¿Las meta-skills respetan las validaciones?**
Sí, ejecutan el mismo protocolo de 7 pasos por cada skill.

**¿Qué pasa si falla una skill en modo meta-skill?**
Se detiene automáticamente y reporta el error.

---

## Formato de Confirmación Estandarizado

Todas las skills usan este formato al completar:

```
[Nivel X: nombre-skill] - COMPLETADO

Resumen:
- Artefacto 1: [descripción]
- Artefacto 2: [descripción]
- Decisión tomada: [descripción]

¿Deseas continuar con [Nivel X+1: siguiente-skill]?
```

Al completar TODAS las skills de un flujo:

```
========================================
FLUJO COMPLETO DE [MÓDULO] TERMINADO
========================================

Todas las skills individuales completadas.

Para futuros módulos similares, puedes usar meta-skills:
- agent-backend (DB + API completo)
- agent-frontend (UI completo)
- agent-fullstack (Backend + Frontend + Tests)

¿Quieres que te explique cómo usar meta-skills?
```

---

## Excepciones a la Confirmación

Estas skills NO requieren confirmación explícita:

- `skill-sync` — Sincronización de metadata interna
- `changelog` — Actualización automática de CHANGELOG.md
- Skills de utilidad que no producen artefactos de negocio principales

---

## Interrupción y Reanudación

### Detener ejecución
```
Usuario: "Detente" o "Para" o "Stop"
```

El asistente completa la skill actual y se detiene.

### Reanudar ejecución
```
Usuario: "Continúa" o "Sigue" o "OK"
```

El asistente continúa con la siguiente skill del flujo.

### Modificar antes de continuar
```
Usuario: "Modifica la tabla embarcacion para agregar columna email"
```

El asistente ajusta el artefacto y vuelve a preguntar si continuar.

---

## Casos de Uso por Modo

### Usa Nivel por Nivel cuando:
- Es tu primera vez implementando un módulo
- El módulo tiene lógica de negocio compleja
- Necesitas revisar decisiones de diseño en cada capa
- Estás aprendiendo el framework
- El proyecto es crítico o regulado

### Usa Meta-Skills cuando:
- Ya implementaste módulos similares antes
- La especificación está 100% clara y aprobada
- Es un módulo CRUD simple
- Tienes confianza en el flujo automático
- Necesitas velocidad sobre control granular

---

## Archivos Relacionados

- [AGENTS.md../../AGENTS.md — Reglas de ejecución del framework
- [SKILL-EXECUTION-PROTOCOL.md](../framework/SKILL-EXECUTION-PROTOCOL.md) — Protocolo de 7 pasos con pausa obligatoria
- [README.md](README.md#inicio-rápido) — Flujo recomendado con confirmaciones
- [SKILLS-MANIFEST.md](../framework/SKILLS-MANIFEST.md) — Catálogo de las 58 skills
- [SKILL-ROUTING.md](../framework/SKILL-ROUTING.md) — Cuándo activar cada skill

---

## Resumen Ejecutivo

**Por defecto**: El framework trabaja **nivel por nivel** con confirmación explícita entre cada skill.

**Para velocidad**: Solicita explícitamente meta-skills como `agent-backend` cuando ya conoces el flujo.

**Para detener**: Di "Detente" y el asistente completará la skill actual antes de pausar.

**Para reanudar**: Di "Continúa" o "Sí" cuando estés listo para la siguiente skill.
