---
name: agent-backend
description: 'Meta-skill: activates all backend skills in sequence for backend-only
  work. Trigger: When implementing a backend feature end-to-end (DB → API → endpoints).'
metadata:
  phase:
  - construction
  layer:
  - backend
  enforcement: recommended
  depends_on:
  - database
  - backend-api
  consumed_by:
  - agent-fullstack
  agent_roles:
  - design-agent
  - delivery-agent
---

## Purpose
This meta-skill activates all backend skills in the correct sequence for backend feature development.
Load each skill in order before generating artifacts.

## Backend Workflow

| Step | Skill | Artifacts |
|------|-------|-----------|
| 1 | `database` | SQL conventions review |
| 2 | `database-modeling` | Table design |
| 3 | `database-sp` | Stored procedures / queries |
| 4 | `database-audit` | Audit columns, soft delete |
| 5 | `data-access` | Handler pattern |
| 6 | `backend-api` | Module structure, endpoints |
| 7 | `api-integration` | DB → API wiring |
| 8 | `error-handling` | Error propagation |
| 9 | `shared-libs` | Shared response types |
| 10 | `swagger` | OpenAPI documentation |

## Sequence Diagram
```
[DB Schema] → [SPs/Queries] → [Handler] → [Endpoint] → [Response] → [Swagger]
```

## How to Use
1. Activate this meta-skill
2. Load each referenced skill in the workflow table order
3. Generate each artifact in sequence
4. Validate before moving to next step

## Quality Gates
After completing each step, verify:
- [ ] Step 1-4 (DB): Tables, SPs tested, error codes documented
- [ ] Step 5-7 (Backend): Handler maps SP results, validation errors translated
- [ ] Step 8-9 (Quality): Error codes consistent, shared types used
- [ ] Step 10 (Docs): All endpoints documented in OpenAPI

## Rollback Scenarios

| Situation | Action |
|-----------|--------|
| SP returns unexpected error | Rollback step 4, verify column types match expected schema |
| Handler mapping breaks existing endpoint | Rollback to step 5, verify return types match SP output |
| OpenAPI spec diverges from implementation | Regenerate spec from actual response types, do not hand-edit spec |
| Breaking DB change needed mid-flow | Rollback to step 2, document migration plan, notify downstream consumers |

## Common Mistakes

- **Skipping database-audit**: Audit columns (created_at, updated_at, deleted_at) must exist in every table. Never skip step 4.
- **Handlers with business logic**: Handlers must only map SP results → API responses. Business logic belongs in stored procedures or a service layer.
- **Missing error codes**: Every SP must return a success/error code. Validate error code coverage before step 8.
- **Mixed response types**: All endpoints in a module must use the same response wrapper. Verify in step 10.
