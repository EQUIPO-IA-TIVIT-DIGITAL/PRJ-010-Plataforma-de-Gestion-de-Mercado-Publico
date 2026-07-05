---
name: app-bootstrap
description: 'Application entry point and module registration patterns. Primary examples
  use .NET 8 Program.cs; patterns apply to Java Spring, Python FastAPI, Node.js. Trigger:
  When creating new API projects, adding modules, or configuring middleware.'
metadata:
  phase:
  - construction
  layer:
  - backend
  enforcement: mandatory
  depends_on:
  - shared-libs
  - backend-api
  consumed_by:
  - agent-backend
  agent_roles:
  - delivery-agent
  validation_profile: architecture-consistency
---

## Critical Rules
| Rule | Type | Rationale |
|------|------|-----------|
| Use shared library extensions | ALWAYS | Standardized infrastructure |
| Register modules with dedicated extension methods | ALWAYS | Consistent DI pattern |
| Middleware order is critical | ALWAYS | Wrong order causes runtime bugs |
| Generate local config files | ALWAYS | Required for local development |
| Store secrets in environment variables, never in files | ALWAYS | Security requirement |

## Service Registration Order (.NET)
```csharp
const string applicationName = "Api{Module}";

// 1. Logging
builder.Host.UseApplicationSerilog(applicationName);

// 2. Database
builder.Services.AddApplicationData(connectionString);

// 3. Authentication context
builder.Services.AddIdentityContext();  // reads auth header into DI

// 4. Validation
builder.Services.AddApplicationValidation();

// 5. Modules (Handlers + Validators)
builder.Services.Add{Module}Module();

// 6. Exception Handling
builder.Services.AddApplicationExceptionHandler(opt => {
    opt.ApplicationName = applicationName;
});

// 7. OpenAPI / Swagger
builder.Services.AddSwaggerGen(...);

// 8. Health Checks
builder.Services.AddHealthChecks();
```

## Middleware Order (.NET, CRITICAL)
```csharp
app.UseSwagger(); app.UseSwaggerUI();   // Swagger BEFORE the chain
app.UseCorrelationId();                 // 1
app.UseInputValidation();               // 2
app.UseApplicationExceptionHandler();   // 3
app.Map{Module}Endpoints();
app.MapHealthChecks("/health");
await app.RunAsync();
```

## Java Spring Boot Equivalent
```java
@SpringBootApplication
public class Application {
    public static void main(String[] args) {
        SpringApplication.run(Application.class, args);
    }
}
// Configuration via application.yml / @ConfigurationProperties
// Exception handling via @ControllerAdvice
// DI via @Service, @Repository, @Component
```

## Python FastAPI Equivalent
```python
app = FastAPI(title="{Module} API", version="v1")
app.add_middleware(CorrelationIdMiddleware)
app.add_middleware(ExceptionHandlerMiddleware)
app.include_router(entity_router, prefix="/api/v1/{entities}")

@app.get("/health")
async def health(): return {"status": "ok"}
```

## Required Configuration Files
| File | Purpose |
|------|---------|
| `appsettings.json` | Base config |
| `appsettings.Local.json` | Local dev overrides (gitignored) |
| `appsettings.Development.json` | Dev environment config |
| `.env.local` (Node/Python) | Local env vars (gitignored) |

## Environment Variables (Never in source code)
| Variable | Purpose |
|----------|---------|
| `ConnectionStrings__Default` | Database connection |
| `Jwt__Secret` or auth config | Auth credentials |
| `ExternalServices__{Name}__BaseUrl` | External API URLs |

## Checklist
- [ ] applicationName set and unique per service
- [ ] Middleware registered in correct order
- [ ] Modules registered in DI
- [ ] Health check endpoint exposed
- [ ] Swagger/OpenAPI accessible in non-production
- [ ] Local config files created (not committed)
- [ ] Secrets from environment variables only
