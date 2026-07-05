---
name: react-hooks
description: 'React hooks patterns: Query, Mutation, Logic hooks, state management
  with TanStack Query. Trigger: When implementing hooks, calling APIs, or managing
  state.'
metadata:
  phase:
  - construction
  layer:
  - frontend
  enforcement: mandatory
  depends_on:
  - react
  - typescript
  consumed_by:
  - api-first-frontend
  - agent-frontend
  - agent-fullstack
  agent_roles:
  - design-agent
  - delivery-agent
  validation_profile: skill-contract
---

## Critical Rules
| Rule | Type | Rationale |
|------|------|-----------|
| Use `enabled` param for conditional queries | ALWAYS | Control when query runs |
| Return `{ verbAction, isPending }` from mutations | ALWAYS | Consistent naming |
| Extract ALL page logic to `use{Feature}Logic` | ALWAYS | Separation of concerns |
| Access data as `data?.data.items` or `data?.data` | ALWAYS | API response structure |
| Never call toast or navigate in mutation hooks | NEVER | Belongs in logic/page layer |

## Hook Types

| Hook Type | Purpose | Returns |
|-----------|---------|---------|
| Query | GET requests | TanStack `useQuery` result |
| Mutation | POST/PUT/DELETE | `{ verbAction, isPending }` |
| Logic | Page state + handlers | Data, State, Handlers |

## Query Hook Pattern
```typescript
export function use{Entity}s(params?: {Entity}QueryParams, enabled = true) {
  return useQuery({
    queryKey: ['{entities}', params],
    queryFn: () => api.get<{Entity}ListResponse>('/entities', { params }),
    enabled,
    staleTime: 0,
  });
}
```

## Mutation Hook Pattern
```typescript
export function useCreate{Entity}() {
  const mutation = useMutation({
    mutationFn: (data: Create{Entity}Request) =>
      api.post<Create{Entity}Response>('/entities', data),
  });

  return {
    create: (data: Create{Entity}Request, options?: { onSuccess?: () => void }) =>
      mutation.mutate(data, { onSuccess: () => options?.onSuccess?.() }),
    isPending: mutation.isPending,
  };
}
```

## Mutation Verb Naming
| Operation | Hook Name | Action Returned |
|-----------|-----------|-----------------|
| Create | `useCreate{Entity}` | `create` |
| Update | `useUpdate{Entity}` | `update` |
| Delete | `useDelete{Entity}` | `delete` |
| Submit | `useSubmit{Entity}` | `submit` |
| Cancel | `useCancel{Entity}` | `cancel` |
| Approve | `useApprove{Entity}` | `approve` |
| Reject | `useReject{Entity}` | `reject` |
| Remove sub | `useRemove{SubEntity}` | `remove` |

## Logic Hook Pattern
```typescript
// Structure: Local state → Queries → Mutations → Derived data → Handlers → return
export function use{Feature}Logic() {
  // 1. Local state
  const [selectedId, setSelectedId] = useState<number | null>(null);
  const [modalOpen, setModalOpen] = useState(false);

  // 2. Queries
  const entitiesQuery = use{Entity}s();

  // 3. Mutations
  const { create, isPending: isCreating } = useCreate{Entity}();

  // 4. Derived data
  const entities = entitiesQuery.data?.data?.items ?? [];

  // 5. Handlers
  const handleCreate = (data: FormData) => {
    create(data, {
      onSuccess: () => {
        toast.success('Created successfully');
        setModalOpen(false);
      },
    });
  };

  return {
    // Data
    entities,
    isLoading: entitiesQuery.isLoading,
    // State
    modalOpen,
    // Handlers
    handleCreate,
    handleOpenModal: () => setModalOpen(true),
    handleCloseModal: () => setModalOpen(false),
    isCreating,
  };
}
```

## API Data Access Patterns
```typescript
// Flat detail: data?.data
// List items: data?.data?.items ?? []
// Pagination: data?.pagination?.totalRecords ?? 0
```

## Modal State Pattern
```typescript
const [modal, setModal] = useState<{ open: boolean; item: EntityType | null }>({
  open: false,
  item: null,
});
// Open with item: setModal({ open: true, item: selectedItem })
// Close: setModal({ open: false, item: null })
```

## Blob Download Hook
```typescript
export function useExport{Entity}() {
  return useMutation({
    mutationFn: (params: ExportParams) =>
      api.get('/entities/export', { params, responseType: 'blob' }),
    onSuccess: (blob) => {
      const url = URL.createObjectURL(blob);
      const a = document.createElement('a');
      a.href = url;
      a.download = 'entities.xlsx';
      a.click();
      URL.revokeObjectURL(url);
    },
  });
}
```

## Vue Composable Equivalent
```typescript
// composables/use{Entity}s.ts
export function use{Entity}s(params?: Ref<QueryParams>) {
  const { data, isLoading } = useQuery({
    queryKey: ['{entities}', params],
    queryFn: () => api.get('/entities', { params: params?.value }),
  });
  return { entities: computed(() => data.value?.items ?? []), isLoading };
}
```
