# Preguntas Frecuentes (FAQ)

Respuestas rápidas a las preguntas más comunes sobre el Framework Agéntico de TIVIT Digital.

---

## Inicio y Activación

### 1. ¿Cómo activo el framework?

**Respuesta**: **Se activa automáticamente**.

El framework está configurado con `applyTo: "**"` en [AGENTS.md](../../AGENTS.md), lo que significa que se carga automáticamente cuando abres el chat de VS Code en este workspace.

**No necesitas escribir ningún comando especial para activarlo.**

---

### 2. ¿Qué escribo en el chat?

**Respuesta**: **Lenguaje natural**. No necesitas conocer comandos técnicos.

**Ejemplos correctos**:
```
"Quiero crear el módulo de Gestión de Contratos"
"En el módulo de Usuarios, agrega el campo Departamento"
"Hay un bug en la búsqueda por email"
"Necesito solo el backend para Notificaciones"
"Revisa la seguridad de este código"
```

**No necesitas**:
```
"Ejecuta la skill api-first-spec"
"Activa agent-fullstack"
"Usa el protocolo de 7 pasos"
```

El framework es inteligente y resuelve qué skills ejecutar según tu petición.

---

### 3. ¿Cómo sé si el framework está funcionando?

**Verifica**:

1. Abre el chat de VS Code (`Ctrl/Cmd + I`)
2. Escribe: "¿Qué skills tienes disponibles?"
3. Deberías ver respuesta mencionando skills del framework

**Si no funciona**:
- Verifica que GitHub Copilot esté activo (ícono en barra inferior)
- Refresca VS Code (`Ctrl/Cmd + R`)
- Ver [TROUBLESHOOTING.md](TROUBLESHOOTING.md)

---

## Skills y Flujos

### 4. ¿Todos los proyectos pasan por las skills `framework-*`?

**Respuesta**: **Sí, siempre**.

Las skills `framework-*` son obligatorias (`enforcement: mandatory`) en todos los proyectos. Definen las decisiones que no pueden saltarse sin excepción registrada:

| Niveles | Fase | Qué decide |
|---------|------|-----------|
| 1-4 | Gobierno y dominio | Multi-tenancy, reglas base, vertical, capacidades, MVP |
| 5-9 | Arquitectura agéntica | 7 capas, memoria, guardrails, RBAC, Kubernetes |
| 10 | Scaffold | Estructura de repos y primer slice funcional |
| 22, 24 | Calidad y operación | Estrategia de pruebas, SLOs, monitoreo |

Sin ellas se construye una app CRUD convencional, no una app de agentes.

**Las skills de stack** (`backend-api`, `react`, `database-sp`, etc.) implementan la parte funcional concreta del módulo (Niveles 14-21).

---

### 5. ¿Tengo que repetir todo el proceso para cada módulo?

**Respuesta**: **NO**.

**Para agregar un segundo módulo**:
```
"Ahora quiero agregar el módulo de Usuarios"
```

El framework ejecuta el mismo flujo automáticamente para el nuevo módulo.

**Para actualizar un módulo existente**:
```
"En el módulo de Contratos, agrega el campo Categoría"
```

El framework **solo actualiza lo que cambió** (DB, API, UI según corresponda).

**No regenera todo el módulo.**

Ver [WORKFLOW-GUIDE.md](WORKFLOW-GUIDE.md) para más detalles.

---

### 6. ¿Qué agente debo invocar?

**Respuesta**: **Opcional**. El framework elige automáticamente.

**Uso normal** (recomendado):
```
"Quiero crear el módulo de Contratos"
```
→ El framework selecciona el agente correcto automáticamente.

**Uso avanzado** (opcional):
```
"@Orchestrator Agent planifica este proyecto"
"@Design Agent diseña la arquitectura"
"@Control Agent revisa la seguridad"
"@Delivery Agent implementa el backend"
```

**Para la mayoría de casos**: No necesitas invocar agentes manualmente.

---

### 7. ¿Cómo sé qué skills se activaron?

**Respuesta**: El agente **siempre lo indica** en su respuesta.

**Ejemplo de respuesta del agente**:
```
[Nivel 1: framework-governance]
Voy a establecer la constitución del proyecto...

[Nivel 1: framework-governance] - COMPLETADO
Resumen:
- Multi-tenancy obligatorio definido
- Principios de seguridad establecidos

¿Deseas continuar con [Nivel 2: framework-discovery]?
```

**Si quieres ver el log después**:
```
"¿Qué skills ejecutaste en el último cambio?"
```

---

## Stack y Tecnologías

### 8. ¿Existe skill de Docker?

**Respuesta**: **Sí**, se llama `docker-local`.

**Qué cubre**:
- docker-compose.yml
- Multi-stage Dockerfiles (.NET, Java, Python, Node.js)
- Networking entre servicios
- Variables de entorno
- .dockerignore

**Cómo usarla**:
```
"Configura Docker para este proyecto"
"Crea docker-compose con PostgreSQL y Redis"
"Dockeriza el backend con multi-stage build"
```

Ver [skills/docker-local/SKILL.md](../skills/docker-local/SKILL.md)

---

### 9. ¿Existe skill de seguridad?

**Respuesta**: **Sí**, se llama `security` (enforcement: **mandatory**).

**Qué cubre**:
- Prevención de SQL Injection (queries parametrizadas)
- Prevención de XSS (sanitización)
- OWASP Top 10
- CORS
- Security headers
- Gestión de secretos
- Rate limiting

**Cómo usarla**:
```
"Revisa la seguridad de este endpoint"
"¿Este código tiene vulnerabilidades?"
"Configura CORS para producción"
```

**Se activa automáticamente** en validaciones de código.

Ver [skills/security/SKILL.md](../skills/security/SKILL.md)

---

### 10. ¿Existe skill de Angular?

**Respuesta**: **No actualmente**. Solo existe `react`.

**Alternativa actual**:
- Usa React (recomendado por TIVIT Digital)
- Los patrones de `react` pueden adaptarse manualmente a Angular

**Roadmap**: Skill de Angular está planificada pero no implementada.

---

### 11. ¿Existe skill de buenas prácticas?

**Respuesta**: **Sí**, se llama `code-review`.

**Qué cubre**:
- Checklist pre-PR (blockers, warnings, best practices)
- Validaciones por capa (DB, Backend, Frontend)
- Problemas comunes (memory leaks, N+1 queries, hardcoded values)
- Accessibility, tipos estrictos, error handling

**Cómo usarla**:
```
"Revisa este código antes de hacer commit"
"Code review del módulo de Contratos"
"¿Este código cumple las buenas prácticas?"
```

**Se activa automáticamente** antes de crear PRs.

Ver [skills/code-review/SKILL.md](../skills/code-review/SKILL.md)

---

### 12. ¿Existe skill de documentación?

**Respuesta**: **Parcialmente**.

**Skills de documentación existentes**:
- `readme` — Templates para README de módulos
- `swagger` — Documentación OpenAPI/Swagger automática
- `api-catalog` — Inventario completo de APIs
- `changelog` — Gestión de cambios

**Falta** (en roadmap):
- Documentación técnica automatizada (ADR, diagramas de arquitectura)

---

## Problemas Técnicos

### 13. El framework no responde

**Causas comunes**:
1. **Copilot no activo**: Verifica ícono en barra inferior de VS Code
2. **Sin conexión**: Copilot requiere internet
3. **Workspace incorrecto**: Asegúrate de estar en el workspace correcto

**Soluciones**:
```bash
# Recargar VS Code
Ctrl/Cmd + R

# Verificar Copilot
Clic en ícono de Copilot → Ver estado

# Reabrir workspace
Archivo → Abrir workspace reciente
```

Ver [TROUBLESHOOTING.md](TROUBLESHOOTING.md) para más detalles.

---

### 14. Skills equivocadas se activaron

**Solución**: Dile al agente qué NO hacer.

**Ejemplo**:
```
"Solo necesito el backend, no toques el frontend"
"No crees tests aún, solo la estructura base"
"Actualiza solo el campo Categoría, no cambies nada más"
```

El framework ajusta qué skills ejecutar según tu instrucción.

---

### 15. ¿Puedo desactivar una skill?

**Respuesta**: Las skills **mandatory** no pueden desactivarse (governance, security).

**Skills opcional/recommended**: Puedes pedir que no se ejecuten.

**Ejemplo**:
```
"No generes tests por ahora"
"Omite el README por el momento"
```

**No recomendado**: Omitir `code-review`, `security`, `api-first-spec`.

---

## Validación y Calidad

### 16. ¿Cómo funciona la validación?

**Respuesta**: El framework tiene **perfiles de validación** automáticos.

**Perfiles**:
- `documentation` — Valida completitud de docs
- `skill-contract` — Valida metadata de skills
- `architecture-consistency` — Valida coherencia arquitectónica
- `security-review` — Valida controles de seguridad
- `tenant-isolation` — Valida aislamiento multi-tenant
- `governance-review` — Valida cumplimiento de reglas

**Se aplican automáticamente** según la skill ejecutada.

Ver [VALIDATION-PROFILES.md](../framework/VALIDATION-PROFILES.md)

---

### 17. ¿Puedo ejecutar validación manualmente?

**Respuesta**: Sí, pide explícitamente.

**Ejemplos**:
```
"Valida la seguridad de este módulo"
"Revisa que el código cumple las reglas de arquitectura"
"Valida que la documentación está completa"
```

---

## Workflows y Procesos

### 18. ¿Cuánto tiempo toma crear un módulo?

**Tiempos promedio**:

| Tarea | Sin Framework | Con Framework | Ahorro |
|-------|--------------|---------------|--------|
| Módulo CRUD completo | 2-3 días | 15-30 min | **95%** |
| Endpoint + SP + Tests | 4-6 horas | 10-15 min | **92%** |
| Componente UI + Hooks | 3-4 horas | 5-10 min | **90%** |
| Bug fix + tests | 1-2 horas | 5-10 min | **85%** |

---

### 19. ¿Qué pasa si algo falla a mitad del proceso?

**Respuesta**: El framework mantiene estado. Puedes continuar desde donde quedó.

**Ejemplo**:
```
Usuario: "Crea el módulo de Contratos"
Framework: [Crea DB y Backend exitosamente]
Framework: [Error en Frontend por dependencia faltante]

Usuario: "Instala la dependencia y continúa"
Framework: [Resume desde Frontend]
```

**No necesitas empezar de cero.**

---

### 20. ¿Puedo usar el framework en proyectos existentes?

**Respuesta**: **Sí**, con precauciones.

**Recomendado**:
1. Crear **módulos nuevos** con el framework
2. **No regenerar** código existente que ya funciona
3. Usar para **agregar features** incrementalmente

**No recomendado**:
- Regenerar módulos existentes complejos (riesgo de sobrescribir lógica)
- Proyectos legacy sin tests (difícil validar cambios)

---

## Soporte y Contacto

### 21. ¿A quién contacto si tengo problemas?

**Canales de soporte**:

| Tipo de consulta | Contacto |
|------------------|----------|
| **Problemas técnicos** | Manuel Aliaga ([manuel.aliaga@tivit.com](mailto:manuel.aliaga@tivit.com)) |
| **Revisión de diseño** | Miguel Martinez ([miguel.martinez@tivit.com](mailto:miguel.martinez@tivit.com)) |
| **Documentación** | Ver carpeta [.opencode/](../) |
| **Errores comunes** | Ver [TROUBLESHOOTING.md](TROUBLESHOOTING.md) |

---

### 22. ¿Puedo contribuir al framework?

**Respuesta**: Sí, siguiendo el proceso de contribución.

**Pasos**:
1. Lee [skill-creator](../skills/skill-creator/SKILL.md) para crear nuevas skills
2. Usa [SKILL-TEMPLATE.md](../skills/SKILL-TEMPLATE.md) como base
3. Crea PR con la nueva skill
4. Revisión por Control Agent
5. Aprobación de arquitectura

**Skills más solicitadas** (roadmap):
- Skill de Angular
- Skill de documentación técnica
- Skill de diseño UX

---

## Recursos Adicionales

### 23. ¿Dónde encuentro más información?

**Documentación completa**:

| Documento | Propósito |
|-----------|-----------|
| [README.md../../README.md | Punto de entrada general |
| [QUICKSTART.md](QUICKSTART.md) | Tu primer módulo (15 min) |
| [WORKFLOW-GUIDE.md](WORKFLOW-GUIDE.md) | Workflows detallados |
| [SKILLS-MANIFEST.md](../framework/SKILLS-MANIFEST.md) | Catálogo de 58 skills |
| [SKILL-FLOW.md](../framework/SKILL-FLOW.md) | Ejemplo end-to-end |
| [TROUBLESHOOTING.md](TROUBLESHOOTING.md) | Solución de problemas |

---

### 24. ¿Existe un glosario de términos?

**Términos clave**:

| Término | Definición |
|---------|------------|
| **Skill** | Unidad de conocimiento especializado del framework (58 en total) |
| **Agent** | Rol especializado (Orchestrator, Design, Control, Delivery) |
| **Meta-skill** | Skill que ejecuta otras skills (`agent-fullstack`, `agent-backend`) |
| **Enforcement** | Nivel de obligatoriedad (mandatory, recommended, optional) |
| **Phase** | Etapa del desarrollo (governance, discovery, conception, design, etc.) |
| **Layer** | Capa técnica (database, backend, frontend, infrastructure) |
| **MCP** | Model Context Protocol (herramientas externas) |

---

### 25. ¿El framework reemplaza a los desarrolladores?

**Respuesta**: **No**. El framework es una **herramienta de productividad**.

**Lo que hace**:
- Automatiza tareas repetitivas
- Aplica mejores prácticas consistentemente
- Acelera desarrollo de módulos estándar
- Reduce errores comunes

**Lo que NO hace**:
- Decisiones de arquitectura compleja
- Lógica de negocio única
- Diseño UX personalizado
- Resolución de problemas no estándar

**El desarrollador sigue siendo esencial** para:
- Decisiones de diseño
- Lógica de negocio compleja
- Revisión de código
- Resolución de problemas únicos

---

**¿No encontraste tu pregunta?**  
Contacta a [Manuel Aliaga](mailto:manuel.aliaga@tivit.com) o crea un issue en el repositorio interno.
