---
name: readme
description: 'README template for project modules. Trigger: When creating module documentation,
  README files, or project docs.'
metadata:
  phase:
  - operations
  enforcement: recommended
  depends_on: []
  consumed_by: []
  agent_roles:
  - delivery-agent
  - design-agent
  validation_profile: documentation
---

## Critical Rules
| Rule | Type | Rationale |
|------|------|-----------|
| Update README on every module change | ALWAYS | Keep docs current |
| Include all environment variables | ALWAYS | Deployment needs them |
| Document how to run locally | ALWAYS | Onboarding |
| Keep it concise | ALWAYS | Long READMEs don't get read |

## README Sections by Module Type

### Backend Module (.NET / Java / Python / Node.js)
| Section | Required |
|---------|----------|
| Quick Start | |
| Endpoints | |
| Environment Variables | |
| Project Structure | |
| Dependencies | |
| Database (schemas, migrations) | Optional |

### Frontend Module (React / Angular / Vue)
| Section | Required |
|---------|----------|
| Quick Start | |
| Pages/Routes | |
| Environment Variables | |
| Project Structure | |
| Host/Shell Dependencies | (if microfrontend) |

## When to Update README
| Event | Action |
|-------|--------|
| New endpoint added | Add to Endpoints table |
| New env variable | Add to Environment Variables |
| New dependency | Add to Dependencies |
| Structure change | Update Project Structure |
| Module complete | Full review |

## Anti-Patterns
| Don't | Why |
|-------|-----|
| Copy entire API spec | That's what Swagger/OpenAPI is for |
| Document every function | Code should be self-documenting |
| Include sensitive data | Security risk |
| Write tutorials | Keep it reference-style |
