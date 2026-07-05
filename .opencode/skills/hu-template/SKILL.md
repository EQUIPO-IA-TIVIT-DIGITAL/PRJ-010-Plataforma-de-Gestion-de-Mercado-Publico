---
name: hu-template
description: 'Template for writing User Stories (Historias de Usuario) in a standard
  format. Input for API First specs. Follows standard format with acceptance criteria.
  Trigger: When writing user stories, creating HUs, defining requirements.'
metadata:
  phase:
  - inception
  enforcement: mandatory
  depends_on: []
  consumed_by:
  - api-first-spec
  - html-prototype
  agent_roles:
  - design-agent
  - orchestrator-agent
  validation_profile: documentation
---

## Purpose
HU (WHAT the user wants) → API First (HOW to implement it) → Implementation

## HU Template
```markdown
## {REPO_CODE}-{SEQ}: {Descriptive Title}
**Epic:** {Epic Name} | **Layer:** {FRONT / API / BACK / FULL} | **Repo:** {REPO_CODE}

### Historia
**Como** {rol/actor} **Quiero** {acción} **Para** {beneficio}

### Criterios de Aceptación
- [ ] CA1: {Verifiable criterion}

### Reglas de Negocio
| Regla | Descripción |
|-------|-------------|
| RN-001 | {Rule} |

### Datos de Prueba
| Escenario | Input | Output Esperado |
|-----------|-------|-----------------|
| Happy path | {data} | {result} |
| Error | {invalid data} | {message} |

**Prioridad:** {Alta/Media/Baja} | **Estimación:** {S/M/L/XL} | **Sprint:** {number}
```

## HU Numbering and Grouping

### Single-Repo Projects
- **Repo Code** = project code (e.g., `200-034`)
- **Layer** = `FULL` (DB + BACK + FRONT in same repo)
- **Numbering** = `{PROJECT_CODE}-{SEQ}` — single continuous sequence

### Multi-Repo Projects
Convention: `{REPO_CODE}-{SEQUENTIAL}` (three-digit, continuous per repo, never resets)

| Layer | Repo Role |
|-------|-----------|
| **FRONT HOST** | Shell / host app |
| **FRONT DOMAIN** | Domain micro-frontend |
| **API GATEWAY** | Ocelot / BFF / Gateway |
| **BACK CROSS** | Cross-cutting domain API |
| **BACK DOMAIN** | Domain-specific API |

## Acceptance Criteria — SMART
| Attribute | Description |
|-----------|-------------|
| **S**pecific | Clear and unambiguous |
| **M**easurable | Verifiable |
| **A**chievable | Technically feasible |
| **R**elevant | Adds value |
| **T**estable | QA can verify it |

| Bad | Good |
|--------|---------|
| "The system is fast" | "Loads in under 2s" |
| "Works well" | "Shows success message" |

## HU to API First Mapping
| HU Section | API First Section |
|------------|-------------------|
| Criterios de Aceptación | Endpoints |
| Reglas de Negocio | Business Rules + DB |
| Datos de Prueba | Request/Response examples |
| Dependencias (catálogos) | Required Catalogs |
