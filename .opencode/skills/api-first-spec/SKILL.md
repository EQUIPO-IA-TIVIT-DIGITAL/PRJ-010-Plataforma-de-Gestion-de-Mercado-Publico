---
name: api-first-spec
description: 'Generate comprehensive API specification documents per module. Includes:
  Scope, ERD, Catalogs, States, Endpoints, DB objects, DTOs, Business Rules, Error
  Codes. Trigger: When documenting APIs, creating spec documents, or doing API-first
  design.'
metadata:
  phase:
  - inception
  enforcement: mandatory
  depends_on:
  - hu-template
  consumed_by:
  - api-first-backend
  - api-first-frontend
  - api-first-testing
  agent_roles:
  - design-agent
  - orchestrator-agent
  validation_profile: documentation
---

## Purpose
Output: `docs/api-first/{MODULE}.md`

## Document Structure (9 sections)
1. **Scope** (Included/Excluded)
2. **Data Model** (Mermaid ERD + Tables)
3. **Required Catalogs** (Reference tables / enums)
4. **State Flow** (States + Actions matrix)
5. **REST Endpoints** (per endpoint: params, request, response, rules, DB objects)
6. **Database Objects** (Endpoint → SP/Query mapping)
7. **Shared DTOs** (common types: pagination, status item, etc.)
8. **Business Rules** (by category)
9. **Error Codes** (VAL_xxx, BUS_xxx, NOT_FOUND)

## Endpoint Types

| Type | HTTP Pattern | Response Shape | When to Use |
|------|-------------|----------------|-------------|
| List | `GET /resource` | `data.items[]` + `pagination` | Paginated listing |
| Get | `GET /resource/{id}` | `data.item{}` | Single entity detail |
| Create | `POST /resource` | `data.item{}` (201) | New entity |
| Update | `PUT /resource/{id}` | `data.item{}` | Modify existing |
| Delete | `DELETE /resource/{id}` | `data.result{}` | Soft delete |
| Operation | `POST /resource/{id}/{verb}` | `data.item{}` | State transitions |
| Remove | `POST /resource/{id}/sub/{subId}/remove` | `data.result{}` | Remove sub-entity |
| Reorder | `PUT /resource/{id}/sub/reorder` | `data.items[]` | Reorder sub-entities |
| Search | `GET /resource` (with `limit`) | `data.items[]` (no pagination) | Autocomplete |

## Error Code Standard

| Prefix | Use | HTTP |
|--------|-----|------|
| `VAL_` | Input validation | 400 |
| `{MOD}_001` | Not found | 404 |
| `{MOD}_002` | Duplicate/Conflict | 409 |
| `{MOD}_003+` | Business rule | 422 |
| `AUTH_` | Authorization | 403 |
| `SYS_` | System error | 500 |

## Generation Workflow
1. Read HU documents for scope
2. Identify entities and their relationships → ERD
3. Map HU acceptance criteria to endpoints
4. Define request/response shapes per endpoint
5. Map endpoints to DB operations (SP or query)
6. Extract shared DTOs
7. Document business rules and error codes
8. Output spec document

## Post-Creation Tasks
- [ ] Update `docs/api-first/README.md` index
- [ ] Update `docs/API_CATALOG.md` (use `api-catalog` skill)
- [ ] Update `CHANGELOG.md` (use `changelog` skill)
