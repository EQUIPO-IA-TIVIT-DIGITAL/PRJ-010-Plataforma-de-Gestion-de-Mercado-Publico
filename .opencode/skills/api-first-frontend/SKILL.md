---
name: api-first-frontend
description: 'Generate frontend code from OpenAPI spec: TypeScript types, data-fetching
  hooks, base components. Trigger: When implementing frontend from OpenAPI spec, generating
  types from endpoints.'
metadata:
  phase:
  - construction
  layer:
  - frontend
  enforcement: mandatory
  depends_on:
  - api-first-spec
  - react
  - typescript
  consumed_by:
  - agent-frontend
  - agent-fullstack
  agent_roles:
  - design-agent
  - delivery-agent
  validation_profile: skill-contract
---

## Workflow
OpenAPI Spec → Parse → Generate Types → Generate Hooks/Services → Generate Logic → Generate Page

## TypeScript Type Mapping
| OpenAPI Type | TypeScript Type |
|--------------|-----------------|
| `integer` | `number` |
| `string` | `string` |
| `string` (date-time) | `string` |
| `boolean` | `boolean` |
| `array` | `T[]` |
| `object` | `interface` |

**Note:** All nullable fields use `Type | null` (not optional `Type?`). Request types must extend `Record<string, unknown>` when using factory patterns.

## Service IDs (in `types/constants.ts`)
```typescript
export const {Feature}Service = {
  Get{Entity}s: 100,
  Get{Entity}ById: 101,
  PostCreate{Entity}: 102,
  PutUpdate{Entity}: 103,
  Delete{Entity}: 104,
  Post{Verb}{Entity}: 110,  // State transitions
} as const;
```

## Query Hook Pattern (React + TanStack Query)
```typescript
export function use{Entity}s(params?: {Entity}sQueryParams, enabled = true) {
  return useQuery({
    queryKey: ['{entity}s', params],
    queryFn: () => api.get('/entities', { params }),
    enabled,
    staleTime: 0,
  });
}
```

## Mutation Hook Pattern
```typescript
export function useCreate{Entity}() {
  const mutation = useMutation({
    mutationFn: (data: Create{Entity}Request) => api.post('/entities', data),
  });
  return {
    create: (data: Create{Entity}Request, options?: { onSuccess?: () => void }) =>
      mutation.mutate(data, { onSuccess: () => options?.onSuccess?.() }),
    isPending: mutation.isPending,
  };
}
```

## Operation Hook Naming Convention
| Operation | Hook Name | Action Returned |
|-----------|-----------|-----------------|
| Submit | `useSubmit{Entity}` | `submit` |
| Cancel | `useCancel{Entity}` | `cancel` |
| Approve | `useApprove{Entity}` | `approve` |
| Reject | `useReject{Entity}` | `reject` |
| Remove | `useRemove{SubEntity}` | `remove` |

## File Structure
```
features/{feature-name}/
├── components/{Component}/
├── hooks/
│   ├── use{Entity}s.ts         # Query hook
│   ├── useCreate{Entity}.ts    # Mutation hook
│   └── use{Feature}Logic.ts    # Logic orchestrator
├── types/
│   ├── constants.ts            # Service IDs, enums
│   ├── form.types.ts           # Form value types
│   └── index.ts               # Re-exports
├── {Feature}Page.tsx
└── index.ts
```

## Generation Order
1. Service IDs / constants first
2. Types inline with hooks
3. Query hooks (GET endpoints)
4. Mutation hooks (POST/PUT/DELETE/Operation)
5. Logic hook (orchestrates queries + mutations + state)
6. Page component

## Critical Checklist
- [ ] Request types extend `Record<string, unknown>` (if using factory pattern)
- [ ] Query hooks have proper cache keys
- [ ] Mutation hooks do NOT show toast (toast in logic/page layer)
- [ ] Blob mutation generates file download
- [ ] All nullable fields use `Type | null`
