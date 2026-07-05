---
name: dotnet-gateway
description: 'API Gateway patterns with Ocelot (.NET), auth validation, header propagation,
  and distributed tracing. Trigger: When creating or modifying API Gateway, Ocelot
  configuration, or authentication middleware.'
metadata:
  phase:
  - construction
  layer:
  - backend
  enforcement: recommended
  depends_on:
  - app-bootstrap
  - authentication
  consumed_by:
  - agent-backend
  agent_roles:
  - delivery-agent
  - design-agent
  validation_profile: architecture-consistency
---

## Critical Rules
| Rule | Type | Rationale |
|------|------|-----------|
| Validate auth tokens in Gateway, not in each downstream service | ALWAYS | Single validation point |
| Convert auth user to internal identity header for internal APIs | ALWAYS | Standardized identity |
| Propagate X-Correlation-Id to downstream services | ALWAYS | Distributed tracing |
| Use audit logging in Gateway only | ALWAYS | Single audit point |
| Exclude system paths from auth validation | ALWAYS | Health checks, Swagger |
| Upstream routes follow `/{Service}/api/v1/{everything}` pattern | RECOMMENDED | Consistent URL prefix |

## Gateway vs Internal API Responsibilities
| Concern | Gateway | Internal API |
|---------|---------|--------------|
| Token auth validation | YES | NO |
| Audit HTTP log | YES | NO |
| Exception/error log | YES | YES |
| Correlation ID generation | YES | NO |
| Identity header creation | YES (creates it) | YES (reads it) |

## Key Packages (.NET)
| Package | Purpose |
|---------|---------|
| Ocelot | Reverse proxy / gateway |
| MMLib.SwaggerForOcelot | Aggregate swagger from downstream |
| Auth library | Token validation (JWT, custom) |

## Auth Flow
```
Client → [auth token headers] → Gateway → Token validation → User identity → 
Internal header (e.g., HeaderToken) → Ocelot → Internal API → Reads identity from header
```

## Middleware Order (CRITICAL — .NET)
```csharp
app.UseCorrelationId();            // 1
app.UseAuditHttp();                // 2
app.UseExceptionHandler();         // 3
app.UsePathBase("/apigateway");    // 4
app.UseSwagger();                  // 5
app.UseSwaggerForOcelotUI();       // 6
app.UseCors();                     // 7
app.UseRouting(); app.UseAuthorization();
app.MapHealthChecks("/health");    // 8
app.UseAuthValidation();           // 9 ← BEFORE gateway routing
await app.UseOcelot();             // 10 ← LAST
```

## Ocelot Route Pattern
```json
{
  "UpstreamPathTemplate": "/{Module}/api/v1/{everything}",
  "DownstreamPathTemplate": "/api/v1/{everything}",
  "UpstreamHttpMethod": ["GET","POST","DELETE","PUT","PATCH"],
  "SwaggerKey": "Api{Module}"
}
```

## Excluded Paths from Auth
```json
"ExcludePath": ["/swagger", "/health"]
```

## Alternatives to Ocelot
| Alternative | Language | Notes |
|-------------|----------|-------|
| Spring Cloud Gateway | Java | Spring Boot ecosystem |
| Kong / Nginx | Any | Infrastructure-level gateway |
| AWS API Gateway | Any | Cloud-managed |
| Azure API Management | Any | Cloud-managed |
| FastAPI + httpx | Python | Lightweight custom proxy |

## Checklist
- [ ] Auth validation BEFORE gateway routing
- [ ] Audit logging in Gateway only
- [ ] Error logging in both Gateway and internal APIs
- [ ] Excluded paths configured
- [ ] Identity header created and forwarded
- [ ] Swagger aggregation configured for all downstream APIs
- [ ] Upstream routes follow standard pattern
