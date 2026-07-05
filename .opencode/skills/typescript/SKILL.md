---
name: typescript
description: 'TypeScript strict patterns and best practices for frontend projects.
  Trigger: When implementing or refactoring TypeScript in .ts/.tsx files.'
metadata:
  phase:
  - construction
  layer:
  - frontend
  enforcement: mandatory
  depends_on: []
  consumed_by:
  - react
  - react-hooks
  - api-first-frontend
  agent_roles:
  - design-agent
  - delivery-agent
  validation_profile: skill-contract
---

## Critical Rules
| Rule | Type | Rationale |
|------|------|-----------|
| Use `const` object + extract type, not direct union | ALWAYS | Single source of truth |
| Use flat interfaces (one level deep) | ALWAYS | Avoid deeply nested types |
| Never use `any` | NEVER | Type safety |
| Use `Type | null` not `Type?` for nullable API fields | ALWAYS | API returns null, not undefined |
| Use `import type` for type-only imports | ALWAYS | Build optimization |

## Const Types Pattern (REQUIRED)
```typescript
// ALWAYS: const object first, then extract type
const STATUS = { ACTIVE: "active", INACTIVE: "inactive" } as const;
type Status = (typeof STATUS)[keyof typeof STATUS];

// NEVER: Direct union types
type Status = "active" | "inactive";
```

## Flat Interfaces (REQUIRED)
```typescript
// One level deep — nested objects get their own interface
interface UserDetail {
  userId: number;
  name: string;
  status: StatusItem;        // nested → own interface
  address: AddressDetail;    // nested → own interface
}

// NEVER inline nested objects
interface UserDetail {
  status: { id: number; name: string; };  // wrong!
}
```

## Never Use `any`
```typescript
// Use unknown for truly unknown types
function parse(input: unknown): User { ... }

// Use generics for flexible types
function wrap<T>(value: T): Response<T> { ... }

// NEVER
const data: any = fetchData();
```

## Utility Types to Prefer
| Utility | Usage |
|---------|-------|
| `Pick<T, K>` | Extract subset of properties |
| `Omit<T, K>` | Exclude properties |
| `Partial<T>` | Make all properties optional |
| `Required<T>` | Make all properties required |
| `Readonly<T>` | Immutable type |
| `Record<K, V>` | Dictionary / map type |
| `ReturnType<F>` | Extract return type |
| `Parameters<F>` | Extract parameter types |

## Type Guards
```typescript
function isUser(value: unknown): value is User {
  return typeof value === 'object' && value !== null && 'userId' in value;
}
```

## Standard API Response Type
```typescript
export interface ApiResponse<T> {
  success: boolean;
  data: T;
  message: string | null;
  errors: Array<{
    code: string | null;
    field: string | null;
    message: string | null;
  }> | null;
  pagination: {
    page: number;
    pageSize: number;
    totalRecords: number;
    totalPages: number;
    hasNext: boolean;
    hasPrevious: boolean;
  };
  metadata: Record<string, unknown> | null;
}
```

## Request Types (Mutation factory pattern)
```typescript
// Request types must extend Record<string, unknown> when using factory patterns
export interface CreateEntityRequest extends Record<string, unknown> {
  name: string;
  amount: number;
  statusId: number | null;
}
```

## Nullable Fields Convention
```typescript
// API fields that can be null:
export interface EntityDetail {
  entityId: number;
  name: string;        // always string
  email: string | null;  // nullable
  closedAt: string | null; // nullable date
}
// NOT: email?: string  — this is undefined, not null
```

## Module Declarations (env.d.ts for microfrontends)
```typescript
declare module 'host/factories' {
  export const createServiceQuery: (...args: unknown[]) => unknown;
  export const createServiceMutation: (...args: unknown[]) => unknown;
}
declare module 'host/toast' { ... }
declare module 'host/session' { ... }
```

## Enum Pattern (String Enums)
```typescript
// Use string enums for status values and catalogs
enum EntityStatus {
  Draft = 'DRAFT',
  Active = 'ACTIVE',
  Closed = 'CLOSED',
}
```

## Props Interface
```typescript
// Use readonly for component props
interface {Component}Props {
  readonly entityId: number;
  readonly onClose: () => void;
  readonly data: EntityDetail | null;
}
```
