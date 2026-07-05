---
name: framework-skill-name
description: Usa esta skill para [propósito breve]. Sirve para [decisiones principales], [límites] y [salidas esperadas].
when_to_use:
  - Cuando [contexto 1].
  - Cuando [contexto 2].
  - Cuando [contexto 3].
  - Cuando [contexto 4].
  - Cuando [contexto 5].
version: 1.0
# --- Metadata de routing y gobernanza ---
phase: [governance|discovery|conception|design|platform|implementation|operations]
layer: governance|business|design|infrastructure|implementation|operations
enforcement: mandatory|recommended|optional
depends_on: [framework-skill-a, framework-skill-b]
consumed_by: [framework-skill-x, framework-skill-y]
agent_roles: [orchestrator-agent, design-agent, control-agent, delivery-agent]
validation_profile: documentation|skill-contract|architecture-consistency|security-review|tenant-isolation|release-gate|governance-review
mcp_usage: none|optional|governed
---

# framework-skill-name

## Propósito

Esta skill sirve para [resolver el problema principal de la fase].  
Su función es [explicar qué transforma, define o valida dentro del framework].

## Objetivo

Usa esta skill para responder estas preguntas:

1. ¿[pregunta clave 1]?
2. ¿[pregunta clave 2]?
3. ¿[pregunta clave 3]?
4. ¿[pregunta clave 4]?
5. ¿[pregunta clave 5]?

## Relación con otras skills

- `[skill previa o upstream]` aporta [entrada o restricción].
- `[skill paralela]` comparte dependencias o límites con esta skill.
- `[skill siguiente o downstream]` consume esta skill para [decisión siguiente].
- Esta skill no reemplaza a `[skill vecina]`; solo resuelve [alcance propio].

## Qué debe hacer el agente cuando esta skill está activa

El agente debe:

1. [acción concreta 1].
2. [acción concreta 2].
3. [acción concreta 3].
4. [acción concreta 4].
5. [acción concreta 5].
6. [acción concreta 6].
7. [acción concreta 7].
8. [acción concreta 8].

## Entradas esperadas

Esta skill asume que ya existe:
- [entrada 1];
- [entrada 2];
- [entrada 3];
- [entrada 4].

Si falta esta base, la skill debe pedirla antes de concluir.

## Alcance de la fase

La fase sí incluye:
- [alcance incluido 1];
- [alcance incluido 2];
- [alcance incluido 3];
- [alcance incluido 4].

La fase no incluye todavía:
- [alcance excluido 1];
- [alcance excluido 2];
- [alcance excluido 3];
- [alcance excluido 4].

## Principios que siempre debe respetar

- [principio 1].
- [principio 2].
- [principio 3].
- [principio 4].
- [principio 5].

## Qué decide esta skill y qué delega

Esta skill sí decide:
- [decisión propia 1];
- [decisión propia 2];
- [decisión propia 3];
- [decisión propia 4].

Esta skill delega:
- [delegación 1] a `[skill dueña]`;
- [delegación 2] a `[skill dueña]`;
- [delegación 3] a `[skill dueña]`.

## Qué debe definir el diseño

### 1. [Bloque principal 1]
Definir:
- [campo];
- [campo];
- [campo];
- [campo].

### 2. [Bloque principal 2]
Definir:
- [campo];
- [campo];
- [campo];
- [campo].

### 3. [Bloque principal 3]
Definir:
- [campo];
- [campo];
- [campo];
- [campo].

### 4. [Bloque principal 4]
Definir:
- [campo];
- [campo];
- [campo];
- [campo].

## Preguntas guía

### 1. Sobre [tema]
- ¿[pregunta]?
- ¿[pregunta]?
- ¿[pregunta]?

### 2. Sobre [tema]
- ¿[pregunta]?
- ¿[pregunta]?
- ¿[pregunta]?

### 3. Sobre [tema]
- ¿[pregunta]?
- ¿[pregunta]?
- ¿[pregunta]?

## Salidas esperadas de esta skill

Cuando esta skill responda, debe producir uno o varios de estos artefactos:

### A. [Artefacto 1]
- [campo];
- [campo];
- [campo];
- [campo].

### B. [Artefacto 2]
- [campo];
- [campo];
- [campo];
- [campo].

### C. [Artefacto 3]
- [campo];
- [campo];
- [campo];
- [campo].

### D. Consumidores de esta skill
- consumidor;
- artefacto consumido;
- decisión habilitada;
- riesgo si falta.

## Criterios de calidad

La skill debe evaluar el diseño usando estos criterios:

- [criterio 1];
- [criterio 2];
- [criterio 3];
- [criterio 4];
- [criterio 5].

## Comportamiento esperado del agente

Cuando [antipatrón o situación], el agente debe [respuesta esperada].  
Cuando [antipatrón o situación], el agente debe [respuesta esperada].  
Cuando [antipatrón o situación], el agente debe [respuesta esperada].  
Cuando [antipatrón o situación], el agente debe [respuesta esperada].

## Plantilla de respuesta recomendada

Usa esta estructura:

1. [sección 1].
2. [sección 2].
3. [sección 3].
4. [sección 4].
5. [sección 5].
6. [sección 6].
7. [sección 7].
8. [sección 8].

## Ejemplos de uso

### Ejemplo 1
Consulta: "[ejemplo de consulta 1]"

Respuesta esperada:
- [respuesta 1];
- [respuesta 2];
- [respuesta 3].

### Ejemplo 2
Consulta: "[ejemplo de consulta 2]"

Respuesta esperada:
- [respuesta 1];
- [respuesta 2];
- [respuesta 3].

## Checklist final de la skill

Antes de cerrar una respuesta, verificar:

- ¿Se definió cuándo se activa la skill?
- ¿Se aclaró qué decide y qué delega?
- ¿Se produjeron artefactos consumibles por otra skill?
- ¿Se mantuvo el alineamiento con gobierno, multi-tenancy y trazabilidad?
- ¿Se documentaron riesgos, vacíos y decisiones pendientes?

## Notas de edición

Usa esta plantilla cuando se cree una skill nueva o cuando una skill existente necesite ser normalizada. Antes de publicarla:

1. sustituir todos los placeholders;
2. confirmar que `name:` coincide con la carpeta;
3. verificar que la skill tenga relación clara con el resto del framework;
4. añadir consumidores explícitos de sus artefactos;
5. revisar que no invada el alcance de otra skill existente.
