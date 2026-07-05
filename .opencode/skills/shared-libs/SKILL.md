---
name: shared-libs
description: 'Shared library patterns: common contracts, exceptions, response wrappers,
  middleware, and utilities. Trigger: When configuring shared libraries, using common
  patterns, or setting up middleware.'
metadata:
  phase:
  - construction
  layer:
  - backend
  enforcement: mandatory
  depends_on: []
  consumed_by:
  - backend-api
  - data-access
  - api-integration
  - app-bootstrap
  agent_roles:
  - design-agent
  - delivery-agent
  validation_profile: architecture-consistency
---

## Critical Rules
| Rule | Type | Rationale |
|------|------|-----------|
| Use typed exceptions for business errors | ALWAYS | Automatic HTTP mapping |
| Use UTC for all logging timestamps | ALWAYS | Distributed tracing consistency |
| Use typed response wrappers, never raw objects | ALWAYS | Consistent API contract |
| Middleware order is critical | ALWAYS | Wrong order causes runtime bugs |
| Use `OkItem<T>/OkList<T>` helper methods | ALWAYS | Prevents wrong response shape |

## Core Libraries Overview
| Library | Purpose | .NET Package Example |
|---------|---------|---------------------|
| `common` | Base contracts: `ApiResponse`, exceptions, error codes | `Shared.Common` |
| `common-api` | API contracts: identity context, pagination, Swagger helpers | `Shared.Common.Api` |
| `common-data` | DB infrastructure: connection, resilience, logging stores | `Shared.Common.Data` |
| `common-logging` | Structured logging + correlation ID middleware | `Shared.Common.Logging` |
| `common-inspection` | Exception middleware + HTTP audit middleware | `Shared.Common.Inspection` |
| `common-validation` | Validation registration + input validation | `Shared.Common.Validation` |
| `auth` | Auth HTTP client + identity propagation | `Shared.Auth` |
| `storage` | Cloud storage (S3, Azure Blob) presigned URLs | `Shared.Storage` |

## ApiResponse — Standard Contract
```csharp
// Typed wrappers (ALWAYS use these):
ApiResponse<T>.Ok(T data, string message = "Success");
ApiResponse.OkItem<T>(T item);
ApiResponse.OkList<T>(List<T> items);
ApiResponse.OkList<T>(List<T> items, PaginationResult pagination);

// NEVER use ApiResponse<object>
```

## Exception Hierarchy
| Exception | HTTP | Usage |
|-----------|------|-------|
| `ValidationException` | 400 | Input and business validation failures |
| `ForbiddenException` | 403 | Authorization failures |
| `NotFoundException` | 404 | Resource not found |
| `ConflictException` | 409 | Duplicate or state conflict |
| `BusinessRuleException` | 422 | Business rule violation |
| `BadGatewayException` | 502 | Downstream service failure |

## Error Code → Exception Mapping (SP errors)
| Error pattern | Exception type |
|--------------|----------------|
| `VAL_*` | `ValidationException` |
| `AUTH_*` | `ForbiddenException` |
| `SYS_*` | `InternalException` |
| `*_001` | `NotFoundException` |
| `*_002` | `ConflictException` |
| `*_003+` | `BusinessRuleException` |

## Common Error Codes
- **VAL_**: VAL_001 (required) through VAL_008 (length exceeded)
- **AUTH_**: AUTH_001 (unauthorized), AUTH_002 (token expired), AUTH_003 (insufficient permissions)
- **SYS_**: SYS_001 (internal error)

## Cross-cutting Models
- `PaginationResult`: Page, PageSize, TotalRecords, TotalPages, HasNext, HasPrevious
- `ApiError`: Code, Field, Message
- `IdentityContext`: UserId, Email, Roles, Claims

## Identity Context Pattern
```csharp
// Register: builder.Services.AddIdentityContext();
// Use: [FromServices] IdentityContext identity
var currentUser = identity?.UserId ?? throw new UnauthorizedAccessException();
```

## Rate Limiting Levels
| Level | Effective limit |
|-------|-----------------|
| Disabled | No limiter |
| Low | 50/min per IP |
| Standard | 100/min per IP |
| High | 200/min per IP |
| Critical | 30/min per IP |

## Store Selection by API Type
| API Type | Stores to enable |
|----------|-----------------|
| Internal API | LogHttp |
| Gateway | LogHttp + AuditHttp + AuditEndpoint |
| Worker | LogHttp + LogJob |
