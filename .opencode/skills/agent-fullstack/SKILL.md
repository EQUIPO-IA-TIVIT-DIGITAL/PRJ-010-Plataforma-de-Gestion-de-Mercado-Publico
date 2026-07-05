---
name: agent-fullstack
description: 'Meta-skill: activates all skills for full-stack feature implementation
  (DB → API → UI). Trigger: When implementing a complete feature across all layers.'
metadata:
  phase:
  - construction
  layer:
  - backend
  - frontend
  enforcement: recommended
  depends_on:
  - agent-backend
  - agent-frontend
  consumed_by: []
  agent_roles:
  - design-agent
  - delivery-agent
  - orchestrator-agent
---

## Purpose
Master meta-skill for full-stack feature development. Orchestrates backend and frontend meta-skills.
Backend is always implemented first.

## Full-Stack Workflow

| Step | Skill | Layer | Artifacts |
|------|-------|-------|-----------|
| 1 | `api-first-spec` | Planning | Feature spec document |
| 2 | `database-modeling` | DB | Table design |
| 3 | `database-sp` | DB | Stored procedures |
| 4 | `database-audit` | DB | Audit columns |
| 5 | `data-access` | Backend | Handlers |
| 6 | `backend-api` | Backend | Endpoints |
| 7 | `api-integration` | Backend | Wiring |
| 8 | `error-handling` | Backend | Error flow |
| 9 | `swagger` | Backend | OpenAPI docs |
| 10 | `typescript` | Frontend | Types |
| 11 | `react-hooks` | Frontend | Query/Mutation hooks |
| 12 | `react` | Frontend | Components + Pages |
| 13 | `export-excel` | Both | (if applicable) Export |
| 14 | `api-first-testing` | Testing | E2E tests |

## Sequence Diagram
```
[Spec] → [DB] → [Backend] → [OpenAPI] → [Frontend Types] →
[Hooks] → [Components] → [E2E Tests]
```

## Implementation Order (MANDATORY)
1. **Backend first** — never generate frontend code before backend is complete
2. **Spec driven** — always start with `api-first-spec` if building something new
3. **Types from spec** — generate TypeScript types from OpenAPI spec

## Cross-Layer Consistency Checks
| Check | When |
|-------|------|
| Error codes match DB ↔ Backend ↔ Frontend | After Step 8 |
| Request/Response types match OpenAPI | After Step 9 |
| TypeScript interfaces match OpenAPI schemas | After Step 10 |
| Hooks match endpoint URLs | After Step 11 |

## Conflict Resolution

| Conflict | Resolution |
|----------|------------|
| Spec change mid-implementation | Complete current layer first, then re-spec from `api-first-spec`, never switch layers mid-step |
| Backend contract breaks frontend | Document breaking change, complete backend migration, notify frontend via updated OpenAPI spec |
| Multiple features touch same table | Implement sequentially, not in parallel per table. Use separate SPs per feature. |
| Performance vs correctness | Default to correctness. Optimize only after profiling. Document each optimization decision. |

## Rollback Strategy

| Phase | Rollback point |
|-------|----------------|
| Planning (Step 1) | Discard spec, restart |
| DB (Steps 2-4) | Drop temp tables, restore from migration backup |
| Backend (Steps 5-9) | Revert endpoint files, keep DB changes |
| Frontend (Steps 10-13) | Revert feature folder, keep types |
| Testing (Step 14) | Discard test files, re-run after fix |

## Common Mistakes

- **Parallel implementation**: Never implement backend and frontend in parallel. Always backend first, then frontend.
- **Skipping spec**: Never skip `api-first-spec`. Without a spec, cross-layer consistency is unverifiable.
- **Stale OpenAPI**: After any backend change, regenerate OpenAPI before touching frontend types.
- **Missing E2E**: Never mark a feature complete without Playwright tests covering the happy path.
