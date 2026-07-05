---
name: repo-structure
description: 'Repository naming conventions, project type detection, and repository
  codification. Trigger: When creating a new repository or naming a project.'
metadata:
  phase:
  - inception
  layer:
  - backend
  - frontend
  enforcement: mandatory
  depends_on: []
  consumed_by:
  - project-bootstrap
  - project-architecture
  agent_roles:
  - orchestrator-agent
  - delivery-agent
  validation_profile: architecture-consistency
---

## Critical Rules
| Rule | Type | Rationale |
|------|------|-----------|
| Ask user to confirm repo name before creating | ALWAYS | Irreversible naming |
| Infer project type from user description | ALWAYS | Correct suffix |
| Use lowercase + hyphens for repo names | ALWAYS | URL-friendly |
| Prefix with project/system code | ALWAYS | Namespace isolation |
| Mix suffixes (e.g., both -api and -web in one repo) | NEVER | Single responsibility |

## Project Type Suffixes

| Type | Suffix | When to use |
|------|--------|-------------|
| Single project | *(none)* | One codebase for entire system |
| API / Backend | `-api` | REST API, GraphQL, gRPC services |
| Frontend / Web | `-web` | SPA, SSR web apps |
| Mobile | `-mobile` | iOS / Android / React Native |
| Gateway | `-gateway` | API Gateway, BFF |
| Worker / Consumer | `-worker` | Background jobs, message consumers |
| Library / Shared | `-libs` | Shared packages, SDKs |
| Infrastructure | `-infra` | Terraform, Pulumi, Bicep |
| Tooling | `-tools` | Scripts, CLIs |
| Documentation | `-docs` | Documentation site |

## Naming Convention
```
{PROJECT-CODE}-{descriptor}-{suffix}
```

| Field | Convention | Example |
|-------|------------|---------|
| PROJECT-CODE | Uppercase, 2–5 chars | `ERP`, `CRM`, `BILLING` |
| descriptor | Optional, lowercase-hyphen | `orders`, `user-management` |
| suffix | From table above | `-api`, `-web` |

**Examples:**
- `erp-api` — ERP single API
- `crm-orders-api` — CRM Orders service
- `billing-web` — Billing frontend
- `platform-gateway` — Platform API Gateway
- `auth-libs` — Auth shared libraries

## Monorepo Naming
When multiple services live in one repo:
```
{PROJECT-CODE}/
├── services/
│   ├── orders-api/
│   └── billing-api/
├── apps/
│   └── admin-web/
└── packages/
    └── shared-libs/
```

## Confirmation Required
Before creating a repository structure, confirm with user:
```
Repository name: {proposed-name}
Project type: {inferred-type}
Is this correct? [yes/no]
```

## Multi-Repo vs Monorepo

| Criteria | Multi-Repo | Monorepo |
|----------|------------|----------|
| Team size | Larger, independent | Smaller, coordinated |
| Deployment | Independent | Coordinated |
| Tooling | Standard git | Requires monorepo tools (nx, turborepo) |
| Code sharing | Via package registry | Via direct imports |
