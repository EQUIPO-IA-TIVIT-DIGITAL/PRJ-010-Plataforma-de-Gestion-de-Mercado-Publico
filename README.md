# Framework Agéntico — TIVIT Digital

> Framework interno de skills y agentes para desarrollo asistido por IA

## Información del Proyecto

- **Organización**: TIVIT
- **Área**: Digital
- **Clasificación**: Proyecto privado interno
- **Autor**: Manuel Aliaga ([manuel.aliaga@tivit.com](mailto:manuel.aliaga@tivit.com))
- **Revisado por**: Miguel Martinez ([miguel.martinez@tivit.com](mailto:miguel.martinez@tivit.com))
- **Última actualización**: 23 de mayo de 2026

## Confidencialidad

Este repositorio contiene propiedad intelectual de **TIVIT** y está destinado exclusivamente para uso interno del área Digital. 

**No compartir fuera de TIVIT sin autorización expresa.**

---

## Inicio Rápido

**¿Primera vez usando el framework?**

1. **Instalar y verificar** → Lee [.opencode/docs/QUICKSTART.md](.opencode/docs/QUICKSTART.md) (15 min)
2. **Primer módulo** → Sigue la guía paso a paso
3. **Workflows comunes** → Consulta [.opencode/docs/WORKFLOW-GUIDE.md](.opencode/docs/WORKFLOW-GUIDE.md)
4. **Preguntas frecuentes** → Revisa [.opencode/docs/FAQ.md](.opencode/docs/FAQ.md)

**¿Ya conoces el framework?**

- [Catálogo de Skills](.opencode/framework/SKILLS-MANIFEST.md) — 58 skills disponibles
- [Flujo End-to-End](.opencode/framework/SKILL-FLOW.md) — Ejemplo completo
- [Protocolo de Ejecución](.opencode/framework/SKILL-EXECUTION-PROTOCOL.md) — Cómo usar skills

---

## Estructura del Proyecto

| Carpeta | Descripción |
|---------|-------------|
| `.opencode/` | **Framework agéntico** — 58 skills, 4 agentes, protocolos, documentación operativa |
| `opencode.json` | Configuración principal de OpenCode — MCP servers, agentes, permisos |
| `AGENTS.md` | Instrucciones base del framework para OpenCode |

---

## ¿Qué es este Framework?

Un sistema de **skills y agentes** que permite a desarrolladores de TIVIT Digital construir aplicaciones guiadas por IA de forma consistente, escalable y siguiendo las mejores prácticas.

### Incluye:

- **58 Skills**: 12 framework skills + 46 stack skills (DB, Backend, Frontend, Testing, Proceso)
- **4 Agentes especializados**: Orchestrator, Design, Control, Delivery
- **Activación automática via OpenCode
- **Workflow modular**: Trabaja módulo por módulo sin repetir pasos innecesarios

### Stack Tecnológico Soportado:

| Capa | Tecnologías |
|------|-------------|
| **Backend** | .NET 8, Java Spring Boot, Python FastAPI, Node.js |
| **Frontend** | React 19, TypeScript, Ant Design 5, Rsbuild |
| **Database** | SQL Server, PostgreSQL, MySQL |
| **Testing** | Playwright (E2E), xUnit/.NET, Jest, Pytest |
| **Infraestructura** | Docker, Kubernetes, Ocelot Gateway |
| **Arquitectura** | Vertical Slice, Modular Monolith, Microfrontends |

---

## Documentación

### Para Nuevos Usuarios

| Documento | Tiempo | Descripción |
|-----------|--------|-------------|
| [QUICKSTART.md](.opencode/docs/QUICKSTART.md) | 15 min | Tu primer módulo paso a paso |
| [WORKFLOW-GUIDE.md](.opencode/docs/WORKFLOW-GUIDE.md) | 10 min | Cómo trabajar módulo por módulo |
| [FAQ.md](.opencode/docs/FAQ.md) | 5 min | Respuestas a preguntas comunes |
| [TROUBLESHOOTING.md](.opencode/docs/TROUBLESHOOTING.md) | - | Solución de problemas frecuentes |

### Para Usuarios Avanzados

| Documento | Propósito |
|-----------|-----------|
| [SKILLS-MANIFEST.md](.opencode/framework/SKILLS-MANIFEST.md) | Catálogo completo de las 58 skills con metadata |
| [SKILL-FLOW.md](.opencode/framework/SKILL-FLOW.md) | Flujo end-to-end con ejemplo de pack NOC |
| [SKILL-EXECUTION-PROTOCOL.md](.opencode/framework/SKILL-EXECUTION-PROTOCOL.md) | Protocolo de 7 pasos para ejecutar skills |
| [SKILL-ROUTING.md](.opencode/framework/SKILL-ROUTING.md) | Cuándo y cómo activar cada skill |

### Para Arquitectos y Tech Leads

| Documento | Propósito |
|-----------|-----------|
| [AGENT-MODEL.md](.opencode/framework/AGENT-MODEL.md) | Roles, responsabilidades y límites de agentes |
| [MCP-GOVERNANCE.md](.opencode/framework/MCP-GOVERNANCE.md) | Gobernanza de herramientas MCP externas |
| [VALIDATION-PROFILES.md](.opencode/framework/VALIDATION-PROFILES.md) | Perfiles de validación por skill |
| [HOOKS-AND-GUARDRAILS.md](.opencode/framework/HOOKS-AND-GUARDRAILS.md) | Interceptores y guardrails del framework |

---

## Requisitos Previos

- **OpenCode** — instalado y configurado como herramienta CLI
- **Node.js** 18+ (para servidores MCP)
- **Docker** (opcional, para entornos locales)
- **Git** configurado con credenciales TIVIT
- **.NET SDK 8** / **JDK 17+** / **Python 3.12+** (según stack del proyecto)

---

## Cómo Funciona

El framework se activa **automáticamente** al iniciar OpenCode en este workspace. Las instrucciones base se cargan desde `AGENTS.md` y las skills son auto-descubiertas desde `.opencode/skills/` No necesitas comandos especiales.

### Ejemplo simple:

```
Usuario escribe: "Quiero crear el módulo de Gestión de Contratos"

Framework ejecuta automáticamente:
Define estructura (api-first-spec)
Crea base de datos (database-sp)
Implementa backend (backend-api)
Implementa frontend (react)
Genera tests (agent-qa)
Crea PR (pull-request)

Resultado: Módulo completo en 15-30 minutos
```

**No necesitas saber qué skills ejecutar** — el framework lo resuelve por ti.

---

## Soporte y Contacto

### ¿Necesitas ayuda?

| Tipo de consulta | Contacto |
|------------------|----------|
| **Problemas técnicos** | Manuel Aliaga ([manuel.aliaga@tivit.com](mailto:manuel.aliaga@tivit.com)) |
| **Revisión de diseño** | Miguel Martinez ([miguel.martinez@tivit.com](mailto:miguel.martinez@tivit.com)) |
| **Documentación técnica** | Ver carpeta [.opencode/docs/](.opencode/docs/) |
| **Errores comunes** | Ver [TROUBLESHOOTING.md](.opencode/docs/TROUBLESHOOTING.md) |

---

## Seguridad y Buenas Prácticas

El framework incluye skills de seguridad obligatorias:

- **Prevención de SQL Injection** — Queries parametrizadas siempre
- **Prevención de XSS** — Sanitización de contenido
- **OWASP Top 10** — Controles integrados
- **Gestión de Secretos** — Nunca en código fuente
- **Code Review automatizado** — Checklist antes de cada PR

Ver [security skill](.opencode/skills/security/SKILL.md) para detalles completos.

---

## Métricas y Beneficios

### Tiempo promedio de desarrollo:

| Tarea | Sin Framework | Con Framework | Ahorro |
|-------|--------------|---------------|--------|
| Módulo CRUD completo | 2-3 días | 15-30 min | **95%** |
| Endpoint + SP + Tests | 4-6 horas | 10-15 min | **92%** |
| Componente UI + Hooks | 3-4 horas | 5-10 min | **90%** |
| Code Review + PR | 30-60 min | 5 min | **85%** |

### Consistencia:

- Todos los módulos siguen las mismas convenciones
- Patrones de seguridad aplicados automáticamente
- Documentación actualizada en cada cambio
- Tests incluidos por defecto

---

## Roadmap

### Próximas mejoras:

- [ ] Skill de Angular (actualmente solo React)
- [ ] Skill de documentación técnica automatizada (ADR, diagramas)
- [ ] Skill de diseño UX (wireframes, user flows)
- [ ] Validadores automáticos por perfil
- [ ] Integración con CI/CD de TIVIT

---

## Licencia y Uso

© 2024-2026 TIVIT. Todos los derechos reservados.

**Uso exclusivo interno** del área Digital de TIVIT.  
Prohibida su distribución, copia o uso fuera de TIVIT sin autorización expresa.

---

## Historial de Cambios

Ver [CHANGELOG.md](.opencode/docs/CHANGELOG.md) para el historial completo de cambios del framework.

---

**¿Listo para empezar?** → [QUICKSTART.md](.opencode/docs/QUICKSTART.md)
