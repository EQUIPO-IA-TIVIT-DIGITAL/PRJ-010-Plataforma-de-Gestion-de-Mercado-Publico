---
name: project-bootstrap
description: 'Entry point for onboarding to a new project. Trigger: When starting
  work on a new project, first-time setup, or project orientation.'
metadata:
  phase:
  - inception
  enforcement: recommended
  depends_on: []
  consumed_by: []
  agent_roles:
  - orchestrator-agent
  - delivery-agent
  validation_profile: documentation
---

## Critical Rules
| Rule | Type | Rationale |
|------|------|-----------|
| Complete ALL steps in order | ALWAYS | Each step depends on the previous |
| Don't skip project context | NEVER | Foundation for all work |
| Verify environment before coding | ALWAYS | Need running services |

## Bootstrap Checklist

### Phase 1: Orientation (Read-Only)
```
□ Step 1: Read project-context or README
□ Step 2: Read API catalog or OpenAPI docs
□ Step 3: Understand repo structure (Backend/Frontend/Database)
```

### Phase 2: Setup (If Needed)
```
□ Step 4: Fill project context (if it has placeholder sections)
□ Step 5: Configure database connection / MCP tools
□ Step 6: Generate API Catalog (if missing) — use api-catalog skill
```

### Phase 3: Environment
```
□ Step 7: Start containers / services (e.g., docker compose up -d)
□ Step 8: Verify health: API health check + DB connection + frontend dev server
```

## Common First Tasks
| Task | Skills to Load |
|------|----------------|
| Add new feature | `api-first-spec` → `agent-fullstack` |
| Fix backend bug | `data-access`, `database-sp` |
| Fix frontend bug | `react` or `design-system` |
| Add new endpoint | `backend-api`, `api-integration` |
| Add new DB migration | `database`, `database-sp` |
| Write tests | `playwright`, `api-first-testing` |

## Troubleshooting
| Symptom | Likely Cause | Solution |
|---------|--------------|----------|
| "Connection refused" | Services not running | Start containers/services |
| "SP / procedure not found" | Missing migration | Run DB migration scripts |
| "CORS error" | Gateway misconfigured | Check gateway routing config |
| "Module not found" | Dependencies missing | `npm install` / package restore |
| "Port in use" | Conflicting service | Stop conflicting process |
