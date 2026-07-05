---
name: project-architecture
description: 'Application architecture patterns: Vertical Slice, Modular Monolith,
  Microservices. Covers multi-stack (/.NET/Java/Python/Node) conventions, naming,
  and response shapes. Trigger: When designing project architecture or onboarding
  a new project structure.'
metadata:
  phase:
  - inception
  layer:
  - backend
  - frontend
  enforcement: mandatory
  depends_on:
  - repo-structure
  consumed_by:
  - backend-api
  - react
  - agent-backend
  - agent-frontend
  - agent-fullstack
  agent_roles:
  - design-agent
  - orchestrator-agent
  validation_profile: architecture-consistency
---

## Critical Rules
| Rule | Type | Rationale |
|------|------|-----------|
| Choose ONE architecture style per service | ALWAYS | Avoid mixed concerns |
| Document architecture decisions in ARCHITECTURE.md | ALWAYS | Team alignment |
| Use consistent module naming across layers | ALWAYS | Discoverability |
| Use standardized ApiResponse wrapper | ALWAYS | API consistency |
| Don't mix Modular Monolith and Microservices without clear boundaries | NEVER | Operational complexity |

## Architecture Styles

| Style | Best For | When NOT to use |
|-------|----------|-----------------|
| Modular Monolith | Small–medium teams, single deployment | When you need independent scaling |
| Vertical Slice | Feature-centric apps, CQRS workflows | Heavily shared domain logic |
| Microservices | Large teams, independent scaling/deployment | Early-stage MVPs |
| Monolith | Quick POC, single developer | Production at scale |

## Vertical Slice Structure (Recommended)
Each feature (slice) is self-contained:
```
{Project}/
├── Features/
│   └── {Entity}/
│       ├── Create/
│       │   ├── Create{Entity}Handler.cs
│       │   ├── Create{Entity}Request.cs
│       │   └── Create{Entity}Response.cs
│       ├── Update/
│       ├── Delete/
│       ├── GetById/
│       └── List/
├── Shared/
│   ├── Infrastructure/  (DbContext, Clients)
│   ├── Middleware/
│   └── Extensions/
└── Program.cs
```

## Modular Monolith Structure
```
{Solution}/
├── src/
│   ├── {Module1}/
│   │   ├── API/           (Controllers or Endpoints)
│   │   ├── Application/   (Use cases, Handlers)
│   │   ├── Domain/        (Entities, Value Objects)
│   │   └── Infrastructure/(Data access, External calls)
│   ├── {Module2}/
│   └── Shared/
└── tests/
```

## Microservices Naming

| Type | Pattern | Example |
|------|---------|---------|
| API Service | `{Domain}-api` | `orders-api` |
| Worker/Consumer | `{Domain}-worker` | `orders-worker` |
| Gateway | `{Project}-gateway` | `ecommerce-gateway` |
| BFF | `{Client}-bff` | `mobile-bff` |

## URL Patterns

| Method | URL | Usage |
|--------|-----|-------|
| GET | `/api/{module}/{entity}s` | List |
| GET | `/api/{module}/{entity}s/{id}` | Get by ID |
| POST | `/api/{module}/{entity}s` | Create |
| PUT | `/api/{module}/{entity}s/{id}` | Update |
| DELETE | `/api/{module}/{entity}s/{id}` | Delete |
| GET | `/api/{module}/{entity}s/export` | Export |
| POST | `/api/{module}/{entity}s/search` | Complex search |

## Standard API Response Shape
```json
{
  "success": true,
  "data": { ... },
  "message": null,
  "errors": null,
  "pagination": {
    "page": 1,
    "pageSize": 20,
    "totalRecords": 150,
    "totalPages": 8,
    "hasNext": true,
    "hasPrevious": false
  },
  "metadata": null
}
```

## Error Response Shape
```json
{
  "success": false,
  "data": null,
  "message": "Validation failed",
  "errors": [
    { "code": "VAL_001", "field": "Name", "message": "Name is required" }
  ],
  "pagination": null,
  "metadata": null
}
```

## Module Naming Conventions

| Element | Convention | Example |
|---------|------------|---------|
| Module | PascalCase, noun | `Orders`, `Billing` |
| Feature folder | PascalCase | `CreateOrder`, `ListOrders` |
| Handler | `{Action}{Entity}Handler` | `CreateOrderHandler` |
| Request | `{Action}{Entity}Request` | `CreateOrderRequest` |
| Response | `{Action}{Entity}Response` | `CreateOrderResponse` |
| Service | `I{Domain}Service` + impl | `IOrderService`, `OrderService` |
| Repository | `I{Entity}Repository` + impl | `IOrderRepository` |

## Frontend Architecture (React)
```
src/
├── features/   (Vertical slices by domain)
├── shared/     (Cross-cutting: hooks, components, utils)
├── App.tsx     (Routing only)
└── main.tsx    (Entry point + providers)
```

## ARCHITECTURE.md Required Sections
1. Architecture style chosen and rationale
2. Module/service boundaries
3. Data flow diagram (ASCII or linked image)
4. Tech stack decisions
5. External dependencies / integrations
6. Open decisions (ADRs)
