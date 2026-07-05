---
name: agent-frontend
description: 'Meta-skill: activates all frontend skills in sequence for frontend-only
  work. Trigger: When implementing a frontend feature end-to-end (types → hooks →
  components).'
metadata:
  phase:
  - construction
  layer:
  - frontend
  enforcement: recommended
  depends_on:
  - react
  - typescript
  consumed_by:
  - agent-fullstack
  agent_roles:
  - design-agent
  - delivery-agent
---

## Purpose
This meta-skill activates all frontend skills in the correct sequence for frontend feature development.
Load each skill in order before generating artifacts.

## Frontend Workflow

| Step | Skill | Artifacts |
|------|-------|-----------|
| 1 | `typescript` | Types, interfaces, const patterns |
| 2 | `design-system` | Color tokens, spacing, component wrappers |
| 3 | `react` | Feature folder structure, pages |
| 4 | `react-hooks` | Query hooks, mutation hooks |
| 5 | `api-first-frontend` | Frontend code from spec |
| 6 | `microfrontend` | (if applicable) Module Federation setup |
| 7 | `export-excel` | (if applicable) Export button and hook |
| 8 | `performance` | Caching strategy, placeholderData |

## Sequence Diagram
```
[Types] → [Feature Structure] → [Query Hooks] → [Mutation Hooks] →
[Logic Hook] → [Components] → [Page] → [Export (optional)]
```

## How to Use
1. Activate this meta-skill
2. Load each referenced skill in workflow table order
3. Generate types first, then hooks, then components
4. Never generate UI before types/hooks are defined

## Quality Gates
After each group, verify:
- [ ] Types (Step 1): No `any`, flat interfaces, `T | null` for nullable fields
- [ ] Hooks (Steps 3-4): Query/mutation/logic hooks separated
- [ ] Components (Step 5): CSS Modules used, no inline styles
- [ ] Performance (Step 8): staleTime set, placeholderData on paginated queries
- [ ] Accessibility: All interactive elements have aria labels, keyboard navigation works

## Fallback Patterns

| Situation | Action |
|-----------|--------|
| Backend API not ready | Mock response types in a separate `.mock.ts` file, never couple UI to unfinished API |
| Design system token missing | Use nearest existing token and file a design debt ticket — never use raw color values |
| Microfrontend not configured | Build as standalone feature, extract to microfrontend later — the feature folder structure supports both modes |

## Common Mistakes

- **Types from guesswork**: Never create TypeScript interfaces from assumptions. Always derive from OpenAPI spec or backend DTOs.
- **Hooks too broad**: Split query hooks from mutation hooks. A single hook doing both violates single responsibility.
- **Missing loading/error states**: Every data-fetching component must handle loading, error, and empty states.
- **Export before feature complete**: Implement export (Excel/CSV) only after the main feature is reviewed. It's always optional.
- **CSS-in-JS over CSS Modules**: The framework standard is CSS Modules. Only deviate with documented justification.
