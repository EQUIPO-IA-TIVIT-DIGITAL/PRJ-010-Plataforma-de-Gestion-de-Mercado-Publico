---
name: microfrontend
description: 'Microfrontend setup with Module Federation: Host/Shell exports, Child
  configuration, adapters. Trigger: When setting up microfrontends, creating child
  apps, or using shell exports.'
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
  - agent-frontend
  agent_roles:
  - design-agent
  - delivery-agent
  validation_profile: architecture-consistency
---

## Critical Rules
| Rule | Type | Rationale |
|------|------|-----------|
| Host uses `eager: true` for shared deps | ALWAYS | Immediate load |
| Child uses `eager: false` for shared deps | ALWAYS | Deferred load |
| Child exposes only a single entry point (`./App`) | ALWAYS | Single entry point |
| Use adapters to consume Host exports | ALWAYS | Abstraction layer |
| Match dependency versions Host ↔ Child | ALWAYS | Avoid conflicts |

## Architecture
```
Host/Shell (Layout + Auth + State)
  Exposes: ./factories, ./hooks, ./toast, ./logger, ./session
  Shared: react, react-dom, react-query, UI library
  └── Children: Child A (./App), Child B (./App), Child C (./App)
```

## Host Exports Pattern

| Export | Functions |
|--------|-----------|
| `./factories` | `createServiceQuery`, `createServiceMutation`, `createBlobMutation` |
| `./hooks` | `useErrorHandler`, `useConfirm`, `useFileUpload`, `useDownloadFile` |
| `./toast` | `toast.success/error/info/warning/loading/dismiss()` |
| `./logger` | `logger.log/warn/error/info/debug()` |
| `./session` | `useCurrentUser`, `useCurrentProfile`, `useAuthState`, `getAuthHeaders` |

## Naming Conventions

| Field | Location | Convention | Example |
|-------|----------|------------|---------|
| `name` | `rsbuild.config.ts` / `vite.config.ts` | English lowercase | `cases` |
| `name` (display) | config/registry | Display name | `Cases` |
| `path` | registry | English lowercase | `cases` |
| Package name | `package.json` | `mf-{module}-remote` | `mf-cases-remote` |

## Host vs Child Configuration

| Aspect | Host | Child |
|--------|------|-------|
| `eager` | `true` | `false` |
| `exposes` | factories, hooks, session, etc. | Only `./App` |
| HTML plugin | enabled | disabled (`false`) |
| Port | Fixed (e.g., 3000) | Unique per child |

## Shared Dependencies (Host eager:true)
```json
{
  "react": { "eager": true, "singleton": true },
  "react-dom": { "eager": true, "singleton": true },
  "@tanstack/react-query": { "eager": true, "singleton": true },
  "react-router-dom": { "eager": true, "singleton": true }
}
```

## Shared Dependencies (Child eager:false)
```json
{
  "react": { "eager": false, "singleton": true },
  "react-dom": { "eager": false, "singleton": true },
  "@tanstack/react-query": { "eager": false, "singleton": true }
}
```

## Bootstrap Child (main.tsx) — Minimal
```tsx
// Child main.tsx should be minimal — no providers
// Auth, query client, theme come from Host
import('./App');  // Dynamic import for Module Federation bootstrap
```

## Module Declaration File (env.d.ts)
```typescript
declare module 'host/factories' {
  export const createServiceQuery: (...) => ...;
}
declare module 'host/toast' { ... }
declare module 'host/session' { ... }
```

## Angular Module Federation Alternative
```typescript
// webpack.config.js (Angular)
new ModuleFederationPlugin({
  name: 'cases',
  exposes: { './Module': './src/app/cases/cases.module.ts' },
  shared: { '@angular/core': { singleton: true } }
})
```

## Vue Module Federation Alternative
```typescript
// vite.config.ts (Vue + Vite)
federation({
  name: 'cases',
  exposes: { './App': './src/App.vue' },
  shared: ['vue', 'pinia', '@tanstack/vue-query']
})
```

## Checklist
- [ ] Host eager: true for all shared deps
- [ ] Child eager: false for all shared deps
- [ ] Child exposes only `./App`
- [ ] Adapters created for all Host imports
- [ ] Module declarations in env.d.ts
- [ ] No providers in child main.tsx (inherited from Host)
- [ ] Port unique per child
