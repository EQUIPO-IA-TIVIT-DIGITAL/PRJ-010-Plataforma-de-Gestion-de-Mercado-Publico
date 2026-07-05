---
name: api-catalog
description: 'Generate complete API inventory/catalog documenting all endpoints end-to-end.
  Maps: DB object → API Endpoint → Service ID → Frontend Screen → Route. Trigger:
  When documenting APIs, creating service inventory, onboarding docs.'
metadata:
  phase:
  - operations
  enforcement: recommended
  depends_on:
  - api-first-spec
  - swagger
  consumed_by:
  - project-bootstrap
  agent_roles:
  - delivery-agent
  - design-agent
  validation_profile: documentation
---

## Purpose
Map: Database (SP/Query) → Backend (Endpoint) → Service ID → Frontend (Screen + Route)

## Data Source Locations
| Source | Location |
|--------|----------|
| DB objects | `database/` or `src/migrations/` |
| Endpoints | `src/{module}/features/` or `src/controllers/` |
| Service IDs | Frontend constants or API config |
| Frontend Routes | `src/App.tsx` or routing module |

## Naming Convention Mapping
| Endpoint Name | DB Object | Method | Path |
|---------------|-----------|--------|------|
| `List{Entity}` | `List{Entity}` | GET | `/{entities}` |
| `Get{Entity}` | `Get{Entity}` | GET | `/{entities}/{id}` |
| `Create{Entity}` | `Create{Entity}` | POST | `/{entities}` |
| `Update{Entity}` | `Update{Entity}` | PUT | `/{entities}/{id}` |
| `Delete{Entity}` | `Delete{Entity}` | DELETE | `/{entities}/{id}` |

## Screen Name Convention
| Action | Screen Name |
|--------|-------------|
| List | `{Entity} List` |
| Get | `{Entity} Detail` |
| Create | `Create {Entity}` |
| Update | `Edit {Entity}` |
| Delete | — (no screen) |

## Output: `docs/API_CATALOG.md`
The catalog should contain a table with columns:
- Module / Feature
- DB Object name
- HTTP Method + Path
- Service ID (frontend)
- Frontend Screen
- Route

## When to Update
- New endpoint added
- Endpoint path/method changed
- New frontend screen added
- Service ID changed
- Module renamed

## Checklist
- [ ] All endpoints listed
- [ ] All DB objects mapped to endpoints
- [ ] All service IDs mapped to endpoints
- [ ] All frontend screens mapped to routes
- [ ] Index / README updated
