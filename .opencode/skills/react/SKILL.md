---
name: react
description: 'React architecture: feature folders, types, components, UI patterns,
  routing, state. Trigger: When creating React features, components, or project structure.'
metadata:
  phase:
  - construction
  layer:
  - frontend
  enforcement: mandatory
  depends_on:
  - typescript
  - design-system
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
| Use feature folders structure | ALWAYS | Self-contained modules |
| Prefix custom wrappers distinctly | ALWAYS | Distinguish from base UI library |
| Use CSS Modules for styles | ALWAYS | Scoped styles |
| Extract page logic to `use{Feature}Logic` hook | ALWAYS | Pages only render |
| Use barrel exports (index.ts) | ALWAYS | Clean imports |
| Each component folder MUST have its own `index.ts` | ALWAYS | TypeScript module resolution |
| Use `any` type | NEVER | Type safety |

## Project Structure
```
src/
├── features/
│   └── {feature-name}/
│       ├── components/
│       │   └── {Component}/
│       │       ├── {Component}.tsx
│       │       └── {Component}.module.css
│       ├── hooks/
│       │   ├── use{Entity}.ts          # Query hook
│       │   ├── use{Action}{Entity}.ts  # Mutation hook
│       │   └── use{Feature}Logic.ts   # Logic hook
│       ├── types/
│       │   ├── constants.ts           # Service IDs, enums
│       │   ├── form.types.ts          # Form value types
│       │   └── index.ts              # Re-exports
│       ├── {Feature}Page.tsx          # Page (directly in feature root)
│       ├── {Feature}Page.module.css
│       └── index.ts                  # Barrel export
├── shared/
│   ├── adapters/
│   ├── components/
│   │   └── {ComponentName}/
│   │       ├── {ComponentName}.tsx
│   │       ├── {ComponentName}.module.css
│   │       └── index.ts
│   ├── hooks/
│   ├── types/
│   └── utils/
├── App.tsx                           # Routes only
└── main.tsx
```

## Pages Pattern
Pages are simple — they render a header + delegate to main component:
```tsx
export default function {Feature}Page() {
  const { data, handlers, state } = use{Feature}Logic();
  return (
    <section>
      <{Feature}Main data={data} handlers={handlers} state={state} />
    </section>
  );
}
```

## Component Wrapper Pattern
Wrap UI library components with custom wrappers for consistent styling:
```tsx
// CustomButton.tsx
const CustomButton = forwardRef<HTMLButtonElement, ButtonProps>((props, ref) => (
  <Button ref={ref} size="large" {...props} />
));
CustomButton.displayName = 'CustomButton';
```

## Routing (React Router 7)
```tsx
// App.tsx contains ONLY routes
export default function App() {
  return (
    <Routes>
      <Route path="/" element={<{Feature}Page />} />
      <Route path="/edit" element={<EditPage />} />
    </Routes>
  );
}
```

## State Between Routes
```tsx
// Navigate with state: navigate('edit', { state: { id } })
// Read: const { id } = useLocation().state as { id: number };
```

## Service IDs / Enums
```typescript
// types/constants.ts
export const {Feature}Service = {
  Get{Entity}s: 100,
  Create{Entity}: 101,
} as const;

// Use string enums for status values
export enum {Entity}Status {
  Draft = 'DRAFT',
  Active = 'ACTIVE',
  Closed = 'CLOSED',
}
```

## Angular Equivalent Structure
```
src/app/
├── features/
│   └── {feature}/
│       ├── components/
│       ├── services/
│       ├── models/
│       └── {feature}.module.ts
└── shared/
    ├── components/
    └── services/
```

## Vue Equivalent Structure
```
src/
├── views/               # Page-level components
├── components/          # Reusable UI components
├── composables/         # Composable hooks (useX)
├── stores/              # Pinia state stores
└── types/
```
