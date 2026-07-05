# Implementation Plan: Rediseño Frontend de MPM

**Branch**: `019-rediseno-frontend` | **Status**: PENDIENTE
**Spec**: [spec.md](./spec.md) | **Semana**: Paralelo / sin fecha fija (después de Fase 7 o en tiempo disponible del equipo)

> Ejecutar `/speckit-plan` para completar: research.md, data-model.md, contracts/, quickstart.md, tasks.md

---

## Summary

Actualiza el sistema de diseño (tokens de tipografía, color, espaciado, indicadores de estado) de `src/mpm-web` y lo aplica de forma incremental, pantalla por pantalla, sin tocar lógica de negocio ni endpoints. No compite por prioridad con Alertas, Buscador Inteligente ni Pipeline de Oportunidades — se ejecuta en paralelo o después de cerrar Fase 7.

---

## Technical Context

**Lenguaje**: React 18 + TypeScript, Ant Design 5
**Dependencias nuevas**: Ninguna prevista — se trabaja sobre el theming de Ant Design (`ConfigProvider`/tokens) existente
**Alcance**: Exclusivamente `src/mpm-web` — sin cambios en `MPM.Api` ni módulos backend
**Estimación**: Sin fecha fija, incremental | **Complejidad**: Media

---

## Module Structure

**Sin módulo backend nuevo**. Cambios acotados a frontend:

```text
src/mpm-web/src/
├── styles/                         ← tokens de tema (color, tipografía, espaciado) centralizados
├── components/                     ← componentes compartidos (tablas, filtros, badges de estado) alineados al nuevo tema
└── pages/*.tsx                     ← aplicación incremental, pantalla por pantalla:
    Licitaciones → Análisis → Mensajería → Notificaciones → Catálogos
```

---

## Constitution Check

| Principio | Estado | Justificación |
|---|---|---|
| **I. Modular Monolith** | ✅ N/A | No afecta módulos backend |
| **II. Stored Procedures First** | ✅ N/A | Sin cambios de datos |
| **III. Migraciones SQL** | ✅ N/A | Sin cambios de BD |
| **IV. Multi-Tenancy** | ✅ Sin violación | Sin cambios de auth/tenant |

---

## Orden de ejecución sugerido (pantalla por pantalla, sin bloquear otras fases)

1. Definir tokens de tema compartidos (una sola vez, base para todo lo demás)
2. Licitaciones (pantalla de mayor uso diario)
3. Análisis (dashboard ya complejo, mayor impacto de coherencia visual)
4. Mensajería, Notificaciones, Catálogos (menor uso, se agrupan al final)

## Artefactos pendientes

- [ ] `research.md` — inventario de inconsistencias actuales por pantalla; definición del set de tokens
- [ ] `quickstart.md` — checklist de validación visual por pantalla antes de dar por cerrada cada una
- [ ] `tasks.md` — generado con `/speckit-tasks`, estructurado para poder pausarse entre pantallas sin dejar trabajo a medias
