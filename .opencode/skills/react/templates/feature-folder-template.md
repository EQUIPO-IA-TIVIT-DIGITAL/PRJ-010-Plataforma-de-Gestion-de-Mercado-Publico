# Feature Folder Structure: {FeatureName}

## Layout

```
src/features/{feature-name}/
├── components/
│   ├── {Entity}List/
│   │   ├── {Entity}List.tsx
│   │   ├── {Entity}List.module.css
│   │   └── index.ts
│   ├── {Entity}Form/
│   │   ├── {Entity}Form.tsx
│   │   ├── {Entity}Form.module.css
│   │   └── index.ts
│   └── {Entity}Detail/
│       ├── {Entity}Detail.tsx
│       ├── {Entity}Detail.module.css
│       └── index.ts
├── hooks/
│   ├── use{Entity}List.ts
│   ├── useCreate{Entity}.ts
│   ├── useUpdate{Entity}.ts
│   ├── useDelete{Entity}.ts
│   └── use{Feature}Logic.ts
├── types/
│   ├── index.ts
│   ├── constants.ts
│   └── form.types.ts
├── {FeatureName}Page.tsx
├── {FeatureName}Page.module.css
└── index.ts
```

## File Responsibilities

| File | Responsibility |
|------|----------------|
| `{FeatureName}Page.tsx` | Page component (routing entry point) — delegates to logic hook |
| `use{Feature}Logic.ts` | Orchestrates queries, mutations, local state, handlers |
| `use{Entity}List.ts` | TanStack Query hook for GET list |
| `useCreate{Entity}.ts` | TanStack Mutation hook for POST |
| `useUpdate{Entity}.ts` | TanStack Mutation hook for PUT |
| `useDelete{Entity}.ts` | TanStack Mutation hook for DELETE |
| `components/{Entity}List/` | List component (table/cards) |
| `components/{Entity}Form/` | Form component (create/edit modal) |
| `types/index.ts` | TypeScript interfaces re-exports |
| `types/constants.ts` | Service IDs, enum constants |
| `types/form.types.ts` | Form-specific types |
| `index.ts` | Barrel export for the feature |

## Barrel Export Pattern

```typescript
// index.ts
export { default as {FeatureName}Page } from './{FeatureName}Page';
export { use{Feature}Logic } from './hooks/use{Feature}Logic';
```

## Routing Registration

```typescript
// App.tsx
<Route path="/{feature-name}" element={<{FeatureName}Page />} />
```
